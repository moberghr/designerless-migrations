using System.Diagnostics;
using System.Text;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Moberg.EntityFrameworkCore.DesignerlessMigrations.EndToEnd.Tests.EndToEndFixture))]

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.EndToEnd.Tests;

/// <summary>Packs both packages once under a unique version and starts one PostgreSQL container for the run.</summary>
public sealed class EndToEndFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public static string RepoRoot { get; } = FindRepoRoot();

    public string WorkRoot { get; } = Path.Combine(Path.GetTempPath(), "designerless-e2e", DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..8]);

    public string FeedDirectory => Path.Combine(WorkRoot, "feed");

    // Unique per run, so the NuGet global cache never serves a package packed by an earlier run.
    public string PackageVersion { get; } = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"10.0.0-e2e.{DateTime.UtcNow:yyyyMMddHHmmss}.{Random.Shared.Next(1000, 9999)}");

    /// <summary>A connection string to a new, empty database on the shared container.</summary>
    public string NewDatabase() =>
        _postgres.GetConnectionString().Replace("Database=postgres", $"Database=e2e_{Guid.NewGuid():N}", StringComparison.Ordinal);

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(FeedDirectory);
        var postgres = _postgres.StartAsync();

        foreach (var project in new[] { "Moberg.EntityFrameworkCore.DesignerlessMigrations", "Moberg.EntityFrameworkCore.DesignerlessMigrations.Npgsql" })
        {
            var result = await Command.RunAsync(
                "dotnet",
                ["pack", Path.Combine(RepoRoot, "src", project, project + ".csproj"), "-c", "Release", "-o", FeedDirectory, $"-p:Version={PackageVersion}"],
                RepoRoot);
            result.ShouldSucceed();
        }

        await postgres;
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        FileSystem.DeleteDirectory(WorkRoot);
    }

    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DesignerlessMigrations.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not find the repository root (DesignerlessMigrations.slnx).");
    }
}

/// <summary>
/// A copy of samples/NpgsqlSample in its own git repository, set up as a standalone consumer of the packed packages.
/// </summary>
public sealed class SampleWorkspace
{
    private readonly EndToEndFixture _fixture;
    private readonly Dictionary<string, string> _environment;

    private SampleWorkspace(EndToEndFixture fixture, string root, string connectionString)
    {
        _fixture = fixture;
        Root = root;
        _environment = new()
        {
            ["DesignerlessMigrationsVersion"] = fixture.PackageVersion,
            ["ConnectionStrings__Sample"] = connectionString,
        };
    }

    public string Root { get; }

    public string ProjectDirectory => Path.Combine(Root, "NpgsqlSample");

    public string MigrationsDirectory => Path.Combine(ProjectDirectory, "Migrations");

    public string SnapshotFile => Path.Combine(MigrationsDirectory, "SampleContextModelSnapshot.cs");

    public string ConnectionString => _environment["ConnectionStrings__Sample"];

    public static async Task<SampleWorkspace> CreateAsync(EndToEndFixture fixture)
    {
        var root = Path.Combine(fixture.WorkRoot, "ws-" + Guid.NewGuid().ToString("N")[..8]);
        var samples = Path.Combine(EndToEndFixture.RepoRoot, "samples");

        FileSystem.CopyDirectory(Path.Combine(samples, "NpgsqlSample"), Path.Combine(root, "NpgsqlSample"));
        File.Copy(Path.Combine(samples, "Directory.Build.props"), Path.Combine(root, "Directory.Build.props"));
        File.Copy(Path.Combine(samples, "Directory.Packages.props"), Path.Combine(root, "Directory.Packages.props"));
        File.Copy(Path.Combine(EndToEndFixture.RepoRoot, ".gitattributes"), Path.Combine(root, ".gitattributes"));
        File.WriteAllText(Path.Combine(root, ".gitignore"), "bin/\nobj/\npublish/\n");
        Directory.CreateDirectory(Path.Combine(root, ".config"));
        File.Copy(Path.Combine(EndToEndFixture.RepoRoot, ".config", "dotnet-tools.json"), Path.Combine(root, ".config", "dotnet-tools.json"));
        File.WriteAllText(Path.Combine(root, "global.json"), File.ReadAllText(Path.Combine(EndToEndFixture.RepoRoot, "global.json")));
        File.WriteAllText(Path.Combine(root, "nuget.config"), $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="local" value="{fixture.FeedDirectory}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
              <packageSourceMapping>
                <packageSource key="local"><package pattern="Moberg.EntityFrameworkCore.DesignerlessMigrations*" /></packageSource>
                <packageSource key="nuget.org"><package pattern="*" /></packageSource>
              </packageSourceMapping>
            </configuration>
            """);

        var workspace = new SampleWorkspace(fixture, root, fixture.NewDatabase());

        (await workspace.GitAsync("init", "--quiet")).ShouldSucceed();
        (await workspace.GitAsync("config", "user.name", "e2e")).ShouldSucceed();
        (await workspace.GitAsync("config", "user.email", "e2e@example.com")).ShouldSucceed();
        (await workspace.GitAsync("config", "commit.gpgsign", "false")).ShouldSucceed();
        await workspace.CommitAllAsync("sample");
        (await workspace.DotnetAsync("tool", "restore")).ShouldSucceed();
        (await workspace.DotnetAsync("build", "NpgsqlSample")).ShouldSucceed();

        return workspace;
    }

    public Task<CommandResult> EfAsync(params string[] arguments) =>
        DotnetAsync(["tool", "run", "dotnet-ef", .. arguments, "--project", "NpgsqlSample"]);

    public Task<CommandResult> DotnetAsync(params string[] arguments) => Command.RunAsync("dotnet", arguments, Root, _environment);

    public Task<CommandResult> GitAsync(params string[] arguments) => Command.RunAsync("git", arguments, Root);

    public async Task CommitAllAsync(string message)
    {
        (await GitAsync("add", "-A")).ShouldSucceed();
        (await GitAsync("commit", "--quiet", "--allow-empty", "-m", message)).ShouldSucceed();
    }

    public async Task<string> GitStatusAsync()
    {
        var result = await GitAsync("status", "--porcelain");
        result.ShouldSucceed();

        return result.Output.Trim();
    }

    /// <summary>Adds a Tag entity to the model, so the next migration has real operations and a snapshot change.</summary>
    public void AddTagEntity() =>
        File.WriteAllText(Path.Combine(ProjectDirectory, "Tag.cs"), """
            using Microsoft.EntityFrameworkCore;

            namespace NpgsqlSample;

            public class Tag
            {
                public int Id { get; set; }

                public required string Label { get; set; }
            }

            public partial class SampleContext
            {
                public DbSet<Tag> Tags { get; set; } = null!;
            }
            """);

    public string[] MigrationFiles(string suffix) =>
        Directory.GetFiles(MigrationsDirectory, "*" + suffix + ".cs");

    public string[] DesignerFiles() =>
        Directory.GetFiles(ProjectDirectory, "*.Designer.cs", SearchOption.AllDirectories);

    public override string ToString() => $"{Root} (packages {_fixture.PackageVersion})";
}

public sealed record CommandResult(string Command, int ExitCode, string Output)
{
    public void ShouldSucceed() => ExitCode.ShouldBe(0, $"{Command} failed:\n{Output}");

    public void ShouldFail() => ExitCode.ShouldNotBe(0, $"{Command} was expected to fail:\n{Output}");
}

public static class Command
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    public static async Task<CommandResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        // Don't inherit the test host's MSBuild/SDK context.
        startInfo.Environment.Remove("MSBuildSDKsPath");
        startInfo.Environment.Remove("MSBuildExtensionsPath");
        startInfo.Environment.Remove("MSBUILD_EXE_PATH");

        foreach (var (key, value) in environment ?? new Dictionary<string, string>())
        {
            startInfo.Environment[key] = value;
        }

        using var process = Process.Start(startInfo)!;
        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) { lock (output) { output.AppendLine(e.Data); } } };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) { lock (output) { output.AppendLine(e.Data); } } };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = new CancellationTokenSource(Timeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{fileName} {string.Join(' ', arguments)} did not finish within {Timeout}.\n{output}");
        }

        return new CommandResult($"{fileName} {string.Join(' ', arguments)}", process.ExitCode, output.ToString());
    }
}

public static class FileSystem
{
    public static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            var name = Path.GetFileName(directory);

            if (name is "bin" or "obj")
            {
                continue;
            }

            CopyDirectory(directory, Path.Combine(destination, name));
        }
    }

    public static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        // git marks object files read-only, which blocks Directory.Delete on Windows.
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}
