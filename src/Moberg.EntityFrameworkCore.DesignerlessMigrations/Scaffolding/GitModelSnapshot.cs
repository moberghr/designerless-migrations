using Microsoft.EntityFrameworkCore.Design;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.Scaffolding;

/// <summary>
/// Without Designer.cs files EF cannot rebuild the previous model snapshot when a migration is removed,
/// so the snapshot is restored from git instead: the version from just before the migration was added.
/// </summary>
internal static class GitModelSnapshot
{
    public static byte[] GetContentBefore(string migrationFile, string snapshotFile) =>
        GetContentBefore(migrationFile, snapshotFile, new Git());

    public static byte[] GetContentBefore(string migrationFile, string snapshotFile, Git git)
    {
        var migrationsDirectory = Path.GetDirectoryName(snapshotFile)!;

        EnsureInsideWorkTree(git, migrationsDirectory, migrationFile, snapshotFile);

        var addedInCommit = git.Run(migrationsDirectory, "log", "--diff-filter=A", "--format=%H", "-1", "--", migrationFile).Trim();

        // Uncommitted migration: HEAD still has the snapshot from before it was added.
        var revision = addedInCommit.Length == 0 ? "HEAD" : addedInCommit + "^";

        EnsureOtherMigrationsAreInRevision(git, migrationsDirectory, revision, migrationFile);

        // --filters applies the same conversions as a checkout (line endings per .gitattributes / core.autocrlf).
        return git.RunForBytes(migrationsDirectory, "cat-file", "--filters", $"{revision}:./{Path.GetFileName(snapshotFile)}");
    }

    // git is the only source of the previous snapshot. Without it, say exactly what to do by hand instead of guessing.
    private static void EnsureInsideWorkTree(Git git, string migrationsDirectory, string migrationFile, string snapshotFile)
    {
        string? reason = null;

        try
        {
            git.Run(migrationsDirectory, "rev-parse", "--is-inside-work-tree");
        }
        catch (GitNotFoundException)
        {
            reason = "git was not found on PATH";
        }
        catch (OperationException)
        {
            reason = $"'{migrationsDirectory}' is not inside a git work tree";
        }

        if (reason != null)
        {
            throw new OperationException(
                $"migrations remove restores the model snapshot from git, but {reason}. Nothing was changed. " +
                $"To remove the migration by hand: if it is applied to a database, revert it first with " +
                $"'dotnet ef database update <previous migration>'; then delete '{migrationFile}' and restore '{snapshotFile}' " +
                "to the version from before this migration was added.");
        }
    }

    // The restored snapshot is only right if it already contains every other migration on disk. One that is missing from
    // the revision (uncommitted, or added in the same commit) would keep its file but lose its model changes, so the next
    // scaffold would repeat them. Without Designer files the snapshot right after it cannot be rebuilt, so refuse.
    private static void EnsureOtherMigrationsAreInRevision(Git git, string migrationsDirectory, string revision, string migrationFile)
    {
        var migrationFileName = Path.GetFileName(migrationFile);
        var inRevision = git.Run(migrationsDirectory, "ls-tree", "--name-only", revision, "--", ".")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Path.GetFileName(x))
            .ToHashSet(StringComparer.Ordinal);
        var missing = Directory.GetFiles(migrationsDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(x => Path.GetFileName(x))
            .Where(MigrationFileGuard.IsMigrationFile)
            .Where(x => x != migrationFileName)
            .Where(x => !inRevision.Contains(x))
            .Order(StringComparer.Ordinal)
            .ToList();

        if (missing.Count > 0)
        {
            throw new OperationException(
                $"Cannot remove '{migrationFileName}': the model snapshot would be restored from {revision}, which does not " +
                $"contain {string.Join(", ", missing)}, so their model changes would be lost. Delete those migration files " +
                $"and '{migrationFileName}', restore the snapshot from git (git checkout {revision} -- <snapshot>), and add one combined migration.");
        }
    }
}
