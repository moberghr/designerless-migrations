using System.Text;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.EndToEnd.Tests;

/// <summary>Runs tools/convert-to-designerless.cs with 'dotnet run', as a user would.</summary>
public sealed class ConverterTests : IDisposable
{
    private const string Id = "20260101000000_AddBlogs";

    private static readonly string Script = Path.Combine(EndToEndFixture.RepoRoot, "tools", "convert-to-designerless.cs");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "designerless-converter", Guid.NewGuid().ToString("N"));

    public ConverterTests() => Directory.CreateDirectory(Path.Combine(_directory, "Migrations"));

    private string MigrationFile => Path.Combine(_directory, "Migrations", Id + ".cs");

    private string DesignerFile => Path.Combine(_directory, "Migrations", Id + ".Designer.cs");

    [Fact]
    public async Task Convert_WhenDesignerPair_ThenMovesAttributesKeepsDownBomAndCrLf()
    {
        WriteEfPair(designerMigrationId: Id);

        var result = await RunAsync();

        result.ShouldSucceed();
        File.Exists(DesignerFile).ShouldBeFalse();
        var bytes = await File.ReadAllBytesAsync(MigrationFile, TestContext.Current.CancellationToken);
        bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()).ShouldBeTrue();
        Encoding.UTF8.GetString(bytes[3..]).ShouldBe(Crlf("""
            using System;
            using App.Data;
            using Microsoft.EntityFrameworkCore.Infrastructure;
            using Microsoft.EntityFrameworkCore.Migrations;

            #nullable disable

            namespace App.Migrations
            {
                /// <inheritdoc />
                [DbContext(typeof(AppContext))]
                [Migration("20260101000000_AddBlogs")]
                public partial class AddBlogs : Migration
                {
                    /// <inheritdoc />
                    protected override void Up(MigrationBuilder migrationBuilder)
                    {
                        migrationBuilder.Sql("CREATE TABLE blogs (id int)");
                    }

                    /// <inheritdoc />
                    protected override void Down(MigrationBuilder migrationBuilder)
                    {
                        migrationBuilder.Sql("DROP TABLE blogs");
                    }
                }
            }

            """));
    }

    [Fact]
    public async Task Convert_WhenRemoveDown_ThenDeletesDownWithItsComment()
    {
        WriteEfPair(designerMigrationId: Id);

        var result = await RunAsync("--remove-down");

        result.ShouldSucceed();
        var code = Encoding.UTF8.GetString((await File.ReadAllBytesAsync(MigrationFile, TestContext.Current.CancellationToken))[3..]);
        code.ShouldEndWith(Crlf("""
                    /// <inheritdoc />
                    protected override void Up(MigrationBuilder migrationBuilder)
                    {
                        migrationBuilder.Sql("CREATE TABLE blogs (id int)");
                    }
                }
            }

            """));
        code.ShouldNotContain("Down");
    }

    [Fact]
    public async Task Convert_WhenDesignerIdDoesNotMatchFileName_ThenChangesNothing()
    {
        WriteEfPair(designerMigrationId: "20260101000000_SomethingElse");
        var before = await File.ReadAllBytesAsync(MigrationFile, TestContext.Current.CancellationToken);

        var result = await RunAsync("--remove-down");

        result.ShouldFail();
        result.Output.ShouldContain("Nothing was changed");
        result.Output.ShouldContain("does not match the file name");
        File.Exists(DesignerFile).ShouldBeTrue();
        (await File.ReadAllBytesAsync(MigrationFile, TestContext.Current.CancellationToken)).ShouldBe(before);
    }

    [Fact]
    public async Task Convert_WhenDryRun_ThenReportsAndChangesNothing()
    {
        WriteEfPair(designerMigrationId: Id);
        var before = await File.ReadAllBytesAsync(MigrationFile, TestContext.Current.CancellationToken);

        var result = await RunAsync("--remove-down", "--dry-run");

        result.ShouldSucceed();
        result.Output.ShouldContain($"Would convert {Path.Combine("Migrations", Id + ".cs")}: attributes moved in; added using Microsoft.EntityFrameworkCore.Infrastructure;");
        result.Output.ShouldContain("Down() removed");
        File.Exists(DesignerFile).ShouldBeTrue();
        (await File.ReadAllBytesAsync(MigrationFile, TestContext.Current.CancellationToken)).ShouldBe(before);
    }

    public void Dispose() => FileSystem.DeleteDirectory(_directory);

    private Task<CommandResult> RunAsync(params string[] options) =>
        Command.RunAsync("dotnet", ["run", Script, "--", _directory, .. options], EndToEndFixture.RepoRoot);

    // What EF Core writes for a migration: UTF-8 with BOM, CRLF, the class split across two files.
    private void WriteEfPair(string designerMigrationId)
    {
        WriteWithBom(MigrationFile, """
            using System;
            using Microsoft.EntityFrameworkCore.Migrations;

            #nullable disable

            namespace App.Migrations
            {
                /// <inheritdoc />
                public partial class AddBlogs : Migration
                {
                    /// <inheritdoc />
                    protected override void Up(MigrationBuilder migrationBuilder)
                    {
                        migrationBuilder.Sql("CREATE TABLE blogs (id int)");
                    }

                    /// <inheritdoc />
                    protected override void Down(MigrationBuilder migrationBuilder)
                    {
                        migrationBuilder.Sql("DROP TABLE blogs");
                    }
                }
            }

            """);
        WriteWithBom(DesignerFile, $$"""
            // <auto-generated />
            using System;
            using App.Data;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Infrastructure;
            using Microsoft.EntityFrameworkCore.Migrations;
            using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
            using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

            #nullable disable

            namespace App.Migrations
            {
                [DbContext(typeof(AppContext))]
                [Migration("{{designerMigrationId}}")]
                partial class AddBlogs
                {
                    /// <inheritdoc />
                    protected override void BuildTargetModel(ModelBuilder modelBuilder)
                    {
                        modelBuilder.HasAnnotation("ProductVersion", "10.0.15");
                    }
                }
            }

            """);
    }

    private static void WriteWithBom(string path, string text) =>
        File.WriteAllBytes(path, [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(Crlf(text))]);

    private static string Crlf(string text) => text.ReplaceLineEndings("\r\n");
}
