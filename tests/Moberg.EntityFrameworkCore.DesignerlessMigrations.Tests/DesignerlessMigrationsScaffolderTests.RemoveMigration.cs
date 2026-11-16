using System.Text;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests;

public sealed partial class DesignerlessMigrationsScaffolderTests
{
    private const string PreviousMigrationId = "20260101000000_Initial";
    private const string NewestMigrationId = "20260102000000_AddBlogName";

    [Fact]
    public void RemoveMigration_WhenNotApplied_ThenDeletesMigrationAndRestoresSnapshot()
    {
        using var repo = new TempGitRepository();
        var migrator = new Mock<IMigrator>();
        var (scaffolder, migrationFile) = Arrange(repo, migrator, applied: false);

        var files = scaffolder.RemoveMigration(repo.Root, "App", force: false, language: null, dryRun: false);

        File.Exists(migrationFile).ShouldBeFalse();
        File.ReadAllText(repo.SnapshotFile).ShouldBe("model before");
        files.MigrationFile.ShouldBe(migrationFile);
        files.SnapshotFile.ShouldBe(repo.SnapshotFile);
        migrator.Verify(x => x.Migrate(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void RemoveMigration_WhenAppliedWithoutForce_ThenThrowsAndChangesNothing()
    {
        using var repo = new TempGitRepository();
        var migrator = new Mock<IMigrator>();
        var (scaffolder, migrationFile) = Arrange(repo, migrator, applied: true);

        var exception = Should.Throw<OperationException>(
            () => scaffolder.RemoveMigration(repo.Root, "App", force: false, language: null, dryRun: false));

        exception.Message.ShouldContain("already been applied");
        File.Exists(migrationFile).ShouldBeTrue();
        File.ReadAllText(repo.SnapshotFile).ShouldBe("model after");
        migrator.Verify(x => x.Migrate(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void RemoveMigration_WhenDryRunAndMigrationIsApplied_ThenDoesNotRevertOrChangeFiles()
    {
        using var repo = new TempGitRepository();
        var migrator = new Mock<IMigrator>();
        var (scaffolder, migrationFile) = Arrange(repo, migrator, applied: true);

        scaffolder.RemoveMigration(repo.Root, "App", force: true, language: null, dryRun: true);

        migrator.Verify(x => x.Migrate(It.IsAny<string?>()), Times.Never);
        File.Exists(migrationFile).ShouldBeTrue();
        File.ReadAllText(repo.SnapshotFile).ShouldBe("model after");
    }

    [Fact]
    public void RemoveMigration_WhenForcedAndMigrationIsApplied_ThenRevertsToPreviousAndRestoresSnapshot()
    {
        using var repo = new TempGitRepository();
        var migrator = new Mock<IMigrator>();
        var (scaffolder, migrationFile) = Arrange(repo, migrator, applied: true);

        scaffolder.RemoveMigration(repo.Root, "App", force: true, language: null, dryRun: false);

        migrator.Verify(x => x.Migrate(PreviousMigrationId), Times.Once);
        File.Exists(migrationFile).ShouldBeFalse();
        File.ReadAllText(repo.SnapshotFile).ShouldBe("model before");
    }

    [Fact]
    public void RemoveMigration_WhenForcedAndDatabaseUnreachable_ThenRemovesFromProjectOnly()
    {
        using var repo = new TempGitRepository();
        var migrator = new Mock<IMigrator>();
        var history = new Mock<IHistoryRepository>();
        history.Setup(x => x.GetAppliedMigrations()).Throws(new InvalidOperationException("connection refused"));
        var (scaffolder, migrationFile) = Arrange(repo, migrator, history);

        scaffolder.RemoveMigration(repo.Root, "App", force: true, language: null, dryRun: false);

        migrator.Verify(x => x.Migrate(It.IsAny<string?>()), Times.Never);
        File.Exists(migrationFile).ShouldBeFalse();
        File.ReadAllText(repo.SnapshotFile).ShouldBe("model before");
    }

    [Fact]
    public void RemoveMigration_WhenSnapshotHasBomAndCrLf_ThenRestoresExactBytes()
    {
        using var repo = new TempGitRepository();
        byte[] before = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("// model before\r\nclass X { }\r\n")];
        var (scaffolder, _) = Arrange(repo, new Mock<IMigrator>(), applied: false, snapshotBefore: before);

        scaffolder.RemoveMigration(repo.Root, "App", force: false, language: null, dryRun: false);

        File.ReadAllBytes(repo.SnapshotFile).ShouldBe(before);
    }

    [Fact]
    public void RemoveMigration_WhenNewerMigrationFileIsNotCompiled_ThenThrowsAndChangesNothing()
    {
        using var repo = new TempGitRepository();
        var migrator = new Mock<IMigrator>();
        var (scaffolder, migrationFile) = Arrange(repo, migrator, applied: false);
        var newer = repo.AddMigration("20260303000000_NotYetBuilt");

        var exception = Should.Throw<OperationException>(
            () => scaffolder.RemoveMigration(repo.Root, "App", force: false, language: null, dryRun: false));

        exception.Message.ShouldContain("20260303000000_NotYetBuilt.cs");
        File.Exists(migrationFile).ShouldBeTrue();
        File.Exists(newer).ShouldBeTrue();
        File.ReadAllText(repo.SnapshotFile).ShouldBe("model after");
    }

    [Fact]
    public void RemoveMigration_WhenNotInGitWorkTree_ThenThrowsBeforeTouchingDatabase()
    {
        using var notARepo = new TempGitRepository(initGit: false);
        notARepo.AddMigration(PreviousMigrationId);
        var migrationFile = notARepo.AddMigration(NewestMigrationId);
        notARepo.WriteSnapshot("model after");
        var migrator = new Mock<IMigrator>();
        var history = new Mock<IHistoryRepository>();
        var scaffolder = CreateScaffolder(migrator, history);

        var exception = Should.Throw<OperationException>(
            () => scaffolder.RemoveMigration(notARepo.Root, "App", force: true, language: null, dryRun: false));

        exception.Message.ShouldContain("not inside a git work tree");
        File.Exists(migrationFile).ShouldBeTrue();
        File.ReadAllText(notARepo.SnapshotFile).ShouldBe("model after");
        history.Verify(x => x.GetAppliedMigrations(), Times.Never);
        migrator.Verify(x => x.Migrate(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void RemoveMigration_WhenLeftoverDesignerFileExists_ThenDeletesIt()
    {
        using var repo = new TempGitRepository();
        var (scaffolder, _) = Arrange(repo, new Mock<IMigrator>(), applied: false);
        var designerFile = Path.Combine(repo.MigrationsDirectory, NewestMigrationId + ".Designer.cs");
        File.WriteAllText(designerFile, "// leftover");

        var files = scaffolder.RemoveMigration(repo.Root, "App", force: false, language: null, dryRun: false);

        File.Exists(designerFile).ShouldBeFalse();
        files.MetadataFile.ShouldBe(designerFile);
    }

    // A git repo with the fixture's previous migration and snapshot committed, and its newest compiled migration
    // added on top as an uncommitted file, as right after 'dotnet ef migrations add'.
    private (DesignerlessMigrationsScaffolder Scaffolder, string MigrationFile) Arrange(
        TempGitRepository repo,
        Mock<IMigrator> migrator,
        bool applied,
        byte[]? snapshotBefore = null)
    {
        var history = new Mock<IHistoryRepository>();
        history
            .Setup(x => x.GetAppliedMigrations())
            .Returns(applied ? [new HistoryRow(PreviousMigrationId, "10.0.15"), new HistoryRow(NewestMigrationId, "10.0.15")] : []);

        return Arrange(repo, migrator, history, snapshotBefore);
    }

    private (DesignerlessMigrationsScaffolder Scaffolder, string MigrationFile) Arrange(
        TempGitRepository repo,
        Mock<IMigrator> migrator,
        Mock<IHistoryRepository> history,
        byte[]? snapshotBefore = null)
    {
        repo.AddMigration(PreviousMigrationId);

        if (snapshotBefore != null)
        {
            repo.WriteSnapshot(snapshotBefore);
        }
        else
        {
            repo.WriteSnapshot("model before");
        }

        repo.Commit("previous migration");
        var migrationFile = repo.AddMigration(NewestMigrationId);
        repo.WriteSnapshot("model after");

        return (CreateScaffolder(migrator, history), migrationFile);
    }

    private DesignerlessMigrationsScaffolder CreateScaffolder(Mock<IMigrator> migrator, Mock<IHistoryRepository> history)
    {
        var dependencies = Services.GetRequiredService<MigrationsScaffolderDependencies>();

        // The fixture's compiled migrations must match the ids the arrange step writes to disk.
        dependencies.MigrationsAssembly.Migrations.Keys.ShouldBe([PreviousMigrationId, NewestMigrationId]);

        return new DesignerlessMigrationsScaffolder(dependencies with
        {
            HistoryRepository = history.Object,
            Migrator = migrator.Object,
        });
    }
}
