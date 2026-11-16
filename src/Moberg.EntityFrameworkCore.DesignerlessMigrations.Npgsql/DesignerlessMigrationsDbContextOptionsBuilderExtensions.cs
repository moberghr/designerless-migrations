using Microsoft.EntityFrameworkCore.Migrations;
using Moberg.EntityFrameworkCore.DesignerlessMigrations.Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;

// Same namespace as UseNpgsql, so no extra using is needed where the context is configured.
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.EntityFrameworkCore;
#pragma warning restore IDE0130

/// <summary>Registers designerless migrations support for PostgreSQL.</summary>
public static class DesignerlessMigrationsDbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Makes migrations without a <c>.Designer.cs</c> generate the same SQL as with one. Call it after <c>UseNpgsql(...)</c>
    /// wherever the context is configured: the app that applies migrations, <c>dotnet ef</c>, and tests.
    /// </summary>
    /// <param name="optionsBuilder">The options builder, already configured with <c>UseNpgsql</c>.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static DbContextOptionsBuilder UseDesignerlessMigrations(this DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        var npgsqlAssembly = typeof(NpgsqlMigrationsSqlGenerator).Assembly;

        if (!optionsBuilder.Options.Extensions.Any(x => x.Info.IsDatabaseProvider && x.GetType().Assembly == npgsqlAssembly))
        {
            throw new InvalidOperationException(
                "UseDesignerlessMigrations() supports only PostgreSQL. Call UseNpgsql(...) first, then UseDesignerlessMigrations().");
        }

        return optionsBuilder.ReplaceService<IMigrationsSqlGenerator, DesignerlessNpgsqlMigrationsSqlGenerator>();
    }

    /// <inheritdoc cref="UseDesignerlessMigrations(DbContextOptionsBuilder)" />
    /// <typeparam name="TContext">The context type.</typeparam>
    public static DbContextOptionsBuilder<TContext> UseDesignerlessMigrations<TContext>(this DbContextOptionsBuilder<TContext> optionsBuilder)
        where TContext : DbContext =>
        (DbContextOptionsBuilder<TContext>)UseDesignerlessMigrations((DbContextOptionsBuilder)optionsBuilder);
}
