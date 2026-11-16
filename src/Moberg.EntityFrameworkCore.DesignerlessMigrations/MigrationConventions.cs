using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations;

/// <summary>
/// Checks a context's migrations follow the designerless conventions. Meant for a test that runs on every PR:
/// <code>MigrationConventions.FindViolations(context).ShouldBeEmpty();</code>
/// </summary>
public static class MigrationConventions
{
    private const BindingFlags DeclaredInstanceMethods =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>
    /// Finds every migration in the context's migrations assembly that breaks a convention: one EF would silently skip,
    /// one with a Designer file, one declaring <c>Down</c> (unless allowed), and data operations without column types.
    /// Migrations attributed to another <see cref="DbContext"/> are ignored.
    /// </summary>
    /// <param name="context">A context configured like the one that applies the migrations (same provider and migrations assembly). It does not connect to the database.</param>
    /// <param name="options">Which conventions to enforce; defaults to all of them.</param>
    /// <returns>The violations, ordered by migration type name; empty when every migration follows the conventions.</returns>
    public static IReadOnlyList<MigrationConventionViolation> FindViolations(DbContext context, MigrationConventionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        options ??= new MigrationConventionOptions();

        var contextType = context.GetType();
        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var discovered = migrationsAssembly.Migrations.Values
            .Select(x => x.AsType())
            .ToHashSet();
        var migrationTypes = migrationsAssembly.Assembly.GetTypes()
            .Where(x => x.IsSubclassOf(typeof(Migration)) && !x.IsAbstract)
            .Where(x => x.GetCustomAttribute<DbContextAttribute>() is not { } attribute || attribute.ContextType == contextType)
            .OrderBy(x => x.FullName, StringComparer.Ordinal)
            .ToList();
        var violations = new List<MigrationConventionViolation>();

        foreach (var migrationType in migrationTypes)
        {
            if (!discovered.Contains(migrationType))
            {
                violations.Add(new(
                    migrationType,
                    MigrationConventionRule.NotDiscovered,
                    $"{migrationType.Name}: EF would silently skip it. Add [DbContext(typeof({contextType.Name}))] and [Migration(\"<id>\")] to the class."));
            }

            if (migrationType.GetMethod("BuildTargetModel", DeclaredInstanceMethods, [typeof(ModelBuilder)]) != null)
            {
                violations.Add(new(
                    migrationType,
                    MigrationConventionRule.HasDesignerFile,
                    $"{migrationType.Name}: delete its .Designer.cs file and move [DbContext] and [Migration] into the migration class."));
            }

            var declaresDown = migrationType.GetMethod("Down", DeclaredInstanceMethods, [typeof(MigrationBuilder)]) != null;

            if (declaresDown && !options.AllowDown)
            {
                // An empty Down() is worse than none: reverting would delete the history row and leave the schema in place.
                // Without Down(), EF's base implementation throws, so a revert fails loudly instead.
                violations.Add(new(
                    migrationType,
                    MigrationConventionRule.DeclaresDown,
                    $"{migrationType.Name}: delete the Down() method entirely (don't leave it empty). Migrations are forward-only."));
            }

            if (options.RequireDataOperationColumnTypes)
            {
                var providerName = context.Database.ProviderName
                    ?? throw new InvalidOperationException("The context has no database provider configured.");

                violations.AddRange(FindDataOperationsWithoutColumnTypes(migrationType, providerName, declaresDown));
            }
        }

        return violations;
    }

    // Without a Designer target model EF has no model to look column types up in, so a data operation needs them inline.
    private static IEnumerable<MigrationConventionViolation> FindDataOperationsWithoutColumnTypes(
        Type migrationType,
        string providerName,
        bool declaresDown)
    {
        var migration = (Migration)Activator.CreateInstance(migrationType)!;
        migration.ActiveProvider = providerName;

        var operations = declaresDown
            ? migration.UpOperations.Select(x => ("Up", x)).Concat(migration.DownOperations.Select(x => ("Down", x)))
            : migration.UpOperations.Select(x => ("Up", x));

        foreach (var (method, operation) in operations)
        {
            var description = operation switch
            {
                InsertDataOperation insert when insert.ColumnTypes == null =>
                    $"InsertData into '{insert.Table}' needs columnTypes:",
                UpdateDataOperation update when update.ColumnTypes == null || update.KeyColumnTypes == null =>
                    $"UpdateData on '{update.Table}' needs columnTypes: and keyColumnTypes:",
                DeleteDataOperation delete when delete.KeyColumnTypes == null =>
                    $"DeleteData from '{delete.Table}' needs keyColumnTypes:",
                _ => null,
            };

            if (description != null)
            {
                yield return new(
                    migrationType,
                    MigrationConventionRule.DataOperationWithoutColumnTypes,
                    $"{migrationType.Name}.{method}: {description} without a Designer model EF cannot look the types up.");
            }
        }
    }
}
