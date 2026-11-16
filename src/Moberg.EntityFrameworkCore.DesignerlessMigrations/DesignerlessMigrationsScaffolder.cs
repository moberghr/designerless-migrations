using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Moberg.EntityFrameworkCore.DesignerlessMigrations.Scaffolding;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations;

/// <summary>
/// Scaffolds migrations as a single file: <c>[DbContext]</c> and <c>[Migration]</c> live on the migration class and no
/// <c>.Designer.cs</c> (a full copy of the model for every migration) is written.
/// <para>
/// Without a Designer file there is no per-migration target model, so <c>migrations remove</c> cannot rebuild the
/// previous model snapshot. It restores the snapshot from git instead, and refuses whenever that could be wrong.
/// </para>
/// </summary>
public class DesignerlessMigrationsScaffolder : MigrationsScaffolder
{
    private const string FileExtension = ".cs";

    /// <summary>Initializes a new instance of the <see cref="DesignerlessMigrationsScaffolder"/> class.</summary>
    /// <param name="dependencies">EF's scaffolder dependencies.</param>
    public DesignerlessMigrationsScaffolder(MigrationsScaffolderDependencies dependencies)
        : base(dependencies)
    {
    }

    /// <inheritdoc />
    public override ScaffoldedMigration ScaffoldMigration(
        string migrationName,
        string? rootNamespace,
        string? subNamespace = null,
        string? language = null,
        bool dryRun = false)
    {
        var migration = base.ScaffoldMigration(migrationName, rootNamespace, subNamespace, language, dryRun);

        if (migration.FileExtension != FileExtension)
        {
            throw new OperationException(
                $"Designerless migrations support only C#, but the migration was scaffolded as '{migration.FileExtension}'.");
        }

        var contextType = Dependencies.CurrentContext.Context.GetType();
        var migrationCode = MigrationCodeRewriter.AddMetadataAttributes(migration.MigrationCode, migration.MigrationId, contextType);

        return new ScaffoldedMigration(
            migration.FileExtension,
            migration.PreviousMigrationId,
            migrationCode,
            migration.MigrationId,
            metadataCode: string.Empty,
            migration.MigrationSubNamespace,
            migration.SnapshotCode,
            migration.SnapshotName,
            migration.SnapshotSubnamespace);
    }

    /// <inheritdoc />
    public override MigrationFiles Save(string projectDir, ScaffoldedMigration migration, string? outputDir, bool dryRun)
    {
        ArgumentNullException.ThrowIfNull(migration);

        var files = base.Save(projectDir, migration, outputDir, dryRun);

        // EF always writes the metadata file; ours is empty, so drop it.
        if (!dryRun && files.MetadataFile != null)
        {
            File.Delete(files.MetadataFile);
        }

        files.MetadataFile = null;

        return files;
    }

    /// <summary>
    /// Removes the newest migration and restores the model snapshot from git. Replaces EF's implementation entirely:
    /// EF's would read the (missing) Designer target model and delete the snapshot.
    /// </summary>
    /// <inheritdoc />
    public override MigrationFiles RemoveMigration(
        string projectDir,
        string? rootNamespace,
        bool force,
        string? language,
        bool dryRun)
    {
        var modelSnapshot = Dependencies.MigrationsAssembly.ModelSnapshot
            ?? throw new OperationException("No model snapshot found in the migrations assembly.");

        var migrationIds = Dependencies.MigrationsAssembly.Migrations.Keys.ToList();

        if (migrationIds.Count == 0)
        {
            throw new OperationException("There are no migrations to remove.");
        }

        var migrationId = migrationIds[^1];
        var previousMigrationId = migrationIds.Count > 1 ? migrationIds[^2] : Migration.InitialDatabase;
        var migrationFile = TryGetProjectFile(projectDir, migrationId + FileExtension)
            ?? throw new OperationException($"Could not find '{migrationId}{FileExtension}' under '{projectDir}'.");

        MigrationFileGuard.EnsureNewestOnDisk(migrationFile, migrationId);

        var snapshotName = modelSnapshot.GetType().Name;
        var snapshotFile = TryGetProjectFile(projectDir, snapshotName + FileExtension)
            ?? throw new OperationException($"Could not find the model snapshot file '{snapshotName}{FileExtension}' under '{projectDir}'.");

        // Resolve the snapshot before touching the database, so a git problem fails before anything is reverted.
        var snapshotContent = GitModelSnapshot.GetContentBefore(migrationFile, snapshotFile);

        RevertIfApplied(migrationId, previousMigrationId, force, dryRun);

        ReportInformation($"Removing migration '{migrationId}' and restoring the model snapshot from git.");

        var leftoverDesignerFile = TryGetProjectFile(projectDir, migrationId + ".Designer" + FileExtension);

        if (!dryRun)
        {
            File.Delete(migrationFile);

            // The exact bytes git produces for a checkout: same BOM and line endings as the committed file.
            File.WriteAllBytes(snapshotFile, snapshotContent);

            if (leftoverDesignerFile != null)
            {
                File.Delete(leftoverDesignerFile);
            }
        }

        return new MigrationFiles
        {
            MigrationFile = migrationFile,
            MetadataFile = leftoverDesignerFile,
            SnapshotFile = snapshotFile,
        };
    }

    // Same semantics as EF's RemoveMigration: --force skips the database check if it is unreachable and reverts the
    // migration (runs its Down) if it was applied; without --force an applied migration is an error.
    // Unlike EF's, a dry run never touches the database: it only reports the revert it would do.
    private void RevertIfApplied(string migrationId, string previousMigrationId, bool force, bool dryRun)
    {
        var applied = false;

        try
        {
            applied = Dependencies.HistoryRepository
                .GetAppliedMigrations()
                .Any(x => x.MigrationId.Equals(migrationId, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (force)
        {
            ReportWarning(
                $"Could not check whether '{migrationId}' is applied ({ex.Message}); removing it from the project only.");
        }

        if (!applied)
        {
            return;
        }

        if (!force)
        {
            throw new OperationException(
                $"The migration '{migrationId}' has already been applied to the database. Revert it and try again, or pass --force to revert it.");
        }

        if (dryRun)
        {
            ReportInformation($"Dry run: would revert the database from '{migrationId}' to '{previousMigrationId}'.");

            return;
        }

        Dependencies.Migrator.Migrate(previousMigrationId);
    }

    // EF1001: the operation reporter is marked internal, but it is the only way to write to dotnet ef's output.
    // That dependency is why package versions follow EF Core majors.
#pragma warning disable EF1001
    private void ReportInformation(string message) => Dependencies.OperationReporter.WriteInformation(message);

    private void ReportWarning(string message) => Dependencies.OperationReporter.WriteWarning(message);
#pragma warning restore EF1001
}
