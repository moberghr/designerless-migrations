using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Design;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.Scaffolding;

internal static partial class MigrationFileGuard
{
    /// <summary>A migration's own file name (<c>yyyyMMddHHmmss_Name.cs</c>); excludes Designer and snapshot files.</summary>
    public static bool IsMigrationFile(string fileName) => MigrationFileName().IsMatch(fileName);

    /// <summary>
    /// The migration EF picks for removal is the newest one in the compiled assembly. If a newer migration file exists
    /// on disk, the assembly is stale (e.g. --no-build right after adding a migration) or that file is invisible to EF
    /// (missing [Migration]); removing would then delete the wrong, possibly already deployed, migration.
    /// EF's own scaffolder guards this with the Designer target model, which designerless migrations do not have.
    /// </summary>
    public static void EnsureNewestOnDisk(string migrationFile, string migrationId)
    {
        var newestOnDisk = Directory.GetFiles(Path.GetDirectoryName(migrationFile)!, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(x => Path.GetFileName(x))
            .Where(IsMigrationFile)
            .Order(StringComparer.Ordinal)
            .Last();

        if (newestOnDisk != migrationId + ".cs")
        {
            throw new OperationException(
                $"The newest compiled migration is '{migrationId}', but '{newestOnDisk}' is newer on disk. Build the project " +
                "(don't pass --no-build after adding a migration) and make sure the newer migration has [DbContext] and [Migration].");
        }
    }

    [GeneratedRegex(@"^\d{14}_\w+\.cs$")]
    private static partial Regex MigrationFileName();
}
