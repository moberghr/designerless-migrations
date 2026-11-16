using System.Text;
using Microsoft.EntityFrameworkCore.Design;
using Moberg.EntityFrameworkCore.DesignerlessMigrations.Scaffolding;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests;

public sealed class GitModelSnapshotTests : IDisposable
{
    private readonly TempGitRepository _repo = new();

    public GitModelSnapshotTests()
    {
        _repo.WriteSnapshot("model v1");
        _repo.AddMigration("20260101000000_First");
        _repo.Commit("first migration");
    }

    [Fact]
    public void GetContentBefore_WhenMigrationIsUncommitted_ThenReturnsSnapshotFromHead()
    {
        var migration = _repo.AddMigration("20260202000000_Second");
        _repo.WriteSnapshot("model v2");

        var result = GitModelSnapshot.GetContentBefore(migration, _repo.SnapshotFile);

        Encoding.UTF8.GetString(result).ShouldBe("model v1");
    }

    [Fact]
    public void GetContentBefore_WhenMigrationIsCommitted_ThenReturnsSnapshotFromBeforeThatCommit()
    {
        var migration = _repo.AddMigration("20260202000000_Second");
        _repo.WriteSnapshot("model v2");
        _repo.Commit("second migration");
        _repo.WriteSnapshot("model v2 plus uncommitted edits");

        var result = GitModelSnapshot.GetContentBefore(migration, _repo.SnapshotFile);

        Encoding.UTF8.GetString(result).ShouldBe("model v1");
    }

    [Fact]
    public void GetContentBefore_WhenOlderMigrationIsCommittedAndNewerIsNot_ThenReturnsSnapshotFromHead()
    {
        _repo.AddMigration("20260202000000_Second");
        _repo.WriteSnapshot("model v2");
        _repo.Commit("second migration");
        var migration = _repo.AddMigration("20260303000000_Third");
        _repo.WriteSnapshot("model v3");

        var result = GitModelSnapshot.GetContentBefore(migration, _repo.SnapshotFile);

        Encoding.UTF8.GetString(result).ShouldBe("model v2");
    }

    [Fact]
    public void GetContentBefore_WhenAnotherMigrationIsUncommitted_ThenThrowsNamingIt()
    {
        // HEAD predates both, so restoring it would drop Second's model changes while its file stays.
        _repo.AddMigration("20260202000000_Second");
        _repo.WriteSnapshot("model v2");
        var migration = _repo.AddMigration("20260303000000_Third");
        _repo.WriteSnapshot("model v3");

        var exception = Should.Throw<OperationException>(() => GitModelSnapshot.GetContentBefore(migration, _repo.SnapshotFile));

        exception.Message.ShouldContain("20260202000000_Second.cs");
        exception.Message.ShouldNotContain("20260101000000_First.cs");
    }

    [Fact]
    public void GetContentBefore_WhenAnotherMigrationIsOnlyStaged_ThenThrowsNamingIt()
    {
        var second = _repo.AddMigration("20260202000000_Second");
        _repo.Stage(second);
        var migration = _repo.AddMigration("20260303000000_Third");
        _repo.WriteSnapshot("model v3");

        var exception = Should.Throw<OperationException>(() => GitModelSnapshot.GetContentBefore(migration, _repo.SnapshotFile));

        exception.Message.ShouldContain("20260202000000_Second.cs");
    }

    [Fact]
    public void GetContentBefore_WhenCommitAddedAnotherMigration_ThenThrowsNamingIt()
    {
        _repo.AddMigration("20260202000000_Second");
        var migration = _repo.AddMigration("20260303000000_Third");
        _repo.WriteSnapshot("model v3");
        _repo.Commit("two migrations at once");

        var exception = Should.Throw<OperationException>(() => GitModelSnapshot.GetContentBefore(migration, _repo.SnapshotFile));

        exception.Message.ShouldContain("20260202000000_Second.cs");
    }

    [Fact]
    public void GetContentBefore_WhenSnapshotHasBomAndCrLf_ThenReturnsExactBytes()
    {
        // EF writes the snapshot as UTF-8 with a BOM; a text round trip would drop the BOM or re-encode it.
        byte[] committed = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("// snapshot\r\nclass X { }\r\n")];
        _repo.WriteSnapshot(committed);
        _repo.Commit("snapshot with BOM and CRLF");
        var migration = _repo.AddMigration("20260202000000_Second");
        _repo.WriteSnapshot("model v2");

        var result = GitModelSnapshot.GetContentBefore(migration, _repo.SnapshotFile);

        result.ShouldBe(committed);
    }

    [Fact]
    public void GetContentBefore_WhenGitattributesConvertLineEndings_ThenReturnsCheckoutLineEndings()
    {
        // Stored with LF, checked out with CRLF: --filters must give what a checkout gives, not the stored blob.
        _repo.WriteFile(".gitattributes", "*.cs text eol=crlf\n");
        _repo.WriteSnapshot("line one\nline two\n");
        _repo.Commit("snapshot stored with LF");
        var migration = _repo.AddMigration("20260202000000_Second");
        _repo.WriteSnapshot("model v2");

        var result = GitModelSnapshot.GetContentBefore(migration, _repo.SnapshotFile);

        Encoding.UTF8.GetString(result).ShouldBe("line one\r\nline two\r\n");
    }

    [Fact]
    public void GetContentBefore_WhenNotInsideGitWorkTree_ThenThrowsWithManualSteps()
    {
        using var notARepo = new TempGitRepository(initGit: false);
        notARepo.WriteSnapshot("model v1");
        var migration = notARepo.AddMigration("20260101000000_First");

        var exception = Should.Throw<OperationException>(() => GitModelSnapshot.GetContentBefore(migration, notARepo.SnapshotFile));

        exception.Message.ShouldContain("is not inside a git work tree");
        exception.Message.ShouldContain("Nothing was changed");
        exception.Message.ShouldContain(migration);
        exception.Message.ShouldContain(notARepo.SnapshotFile);
    }

    [Fact]
    public void GetContentBefore_WhenGitIsNotInstalled_ThenThrowsWithManualSteps()
    {
        var migration = _repo.AddMigration("20260202000000_Second");

        var exception = Should.Throw<OperationException>(
            () => GitModelSnapshot.GetContentBefore(migration, _repo.SnapshotFile, new Git("git-that-does-not-exist-4f1c")));

        exception.Message.ShouldContain("git was not found on PATH");
        exception.Message.ShouldContain("dotnet ef database update <previous migration>");
    }

    public void Dispose() => _repo.Dispose();
}
