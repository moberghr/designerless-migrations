using System.Diagnostics;
using System.Text;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests;

/// <summary>A throwaway git repository with a Migrations folder, for tests of the git-based snapshot restore.</summary>
public sealed class TempGitRepository : IDisposable
{
    public TempGitRepository(string snapshotName = "BloggingContextModelSnapshot", bool initGit = true)
    {
        Root = Path.Combine(Path.GetTempPath(), "designerless-git-tests", Guid.NewGuid().ToString("N"));
        MigrationsDirectory = Path.Combine(Root, "Migrations");
        SnapshotFile = Path.Combine(MigrationsDirectory, snapshotName + ".cs");
        Directory.CreateDirectory(MigrationsDirectory);

        if (initGit)
        {
            Git("init", "--quiet");

            // Independent of the machine's global git config: identity, signing and line-ending conversion.
            Git("config", "user.name", "test");
            Git("config", "user.email", "test@example.com");
            Git("config", "commit.gpgsign", "false");
            Git("config", "core.autocrlf", "false");
        }
    }

    public string Root { get; }

    public string MigrationsDirectory { get; }

    public string SnapshotFile { get; }

    public string AddMigration(string migrationId)
    {
        var path = Path.Combine(MigrationsDirectory, migrationId + ".cs");
        File.WriteAllText(path, migrationId);

        return path;
    }

    public void WriteSnapshot(string content) => File.WriteAllText(SnapshotFile, content);

    public void WriteSnapshot(byte[] content) => File.WriteAllBytes(SnapshotFile, content);

    public void WriteFile(string relativePath, string content) => File.WriteAllText(Path.Combine(Root, relativePath), content);

    public void Stage(string path) => Git("add", "--", path);

    public void Commit(string message)
    {
        Git("add", "-A");
        Git("commit", "--quiet", "-m", message);
    }

    public void Dispose()
    {
        if (!Directory.Exists(Root))
        {
            return;
        }

        // git marks object files read-only, which blocks Directory.Delete on Windows.
        foreach (var file in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(Root, recursive: true);
    }

    private void Git(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = Root,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var errorTask = process.StandardError.ReadToEndAsync();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        process.ExitCode.ShouldBe(0, errorTask.Result);
    }
}
