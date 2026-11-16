using Microsoft.EntityFrameworkCore.Design;
using Moberg.EntityFrameworkCore.DesignerlessMigrations.Scaffolding;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests;

public sealed class MigrationFileGuardTests : IDisposable
{
    private const string NewestCompiled = "20261109085913_AddInvoices";

    private readonly string _migrationsDir = Path.Combine(Path.GetTempPath(), "designerless-guard-tests", Guid.NewGuid().ToString("N"));

    public MigrationFileGuardTests()
    {
        Directory.CreateDirectory(_migrationsDir);
        Touch("20261106103519_AddCustomers.cs");
        Touch(NewestCompiled + ".cs");
        Touch("AppContextModelSnapshot.cs");
    }

    [Fact]
    public void EnsureNewestOnDisk_WhenMigrationIsNewestFile_ThenDoesNotThrow()
    {
        var exception = Record.Exception(() => MigrationFileGuard.EnsureNewestOnDisk(PathOf(NewestCompiled + ".cs"), NewestCompiled));

        exception.ShouldBeNull();
    }

    [Fact]
    public void EnsureNewestOnDisk_WhenNewerFileNotCompiled_ThenThrowsNamingIt()
    {
        // Reproduces "migrations add X" followed by "migrations remove --no-build": the assembly still ends at the
        // previous migration, which would otherwise be deleted instead of X.
        Touch("20261112151730_CliCheck.cs");

        var exception = Should.Throw<OperationException>(
            () => MigrationFileGuard.EnsureNewestOnDisk(PathOf(NewestCompiled + ".cs"), NewestCompiled));

        exception.Message.ShouldContain("20261112151730_CliCheck.cs");
    }

    [Fact]
    public void EnsureNewestOnDisk_WhenLeftoverDesignerFileIsNewer_ThenIgnoresIt()
    {
        Touch("20261112151730_CliCheck.Designer.cs");

        var exception = Record.Exception(() => MigrationFileGuard.EnsureNewestOnDisk(PathOf(NewestCompiled + ".cs"), NewestCompiled));

        exception.ShouldBeNull();
    }

    [Theory]
    [InlineData("20261112151730_CliCheck.cs", true)]
    [InlineData("20261112151730_CliCheck.Designer.cs", false)]
    [InlineData("AppContextModelSnapshot.cs", false)]
    [InlineData("2026111215173_TooShort.cs", false)]
    public void IsMigrationFile_WhenFileName_ThenMatchesOnlyMigrationFiles(string fileName, bool expected)
    {
        MigrationFileGuard.IsMigrationFile(fileName).ShouldBe(expected);
    }

    public void Dispose() => Directory.Delete(_migrationsDir, recursive: true);

    private string PathOf(string fileName) => Path.Combine(_migrationsDir, fileName);

    private void Touch(string fileName) => File.WriteAllText(PathOf(fileName), string.Empty);
}
