using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.Npgsql.Tests;

/// <summary>
/// Every operation is generated with <c>model: null</c>, exactly as at runtime for a migration without a Designer file.
/// </summary>
public class DesignerlessNpgsqlMigrationsSqlGeneratorTests
{
    [Fact]
    public void Generate_WhenAlterColumnOnlyChangesNullability_ThenDoesNotAlterType()
    {
        var sql = GenerateSql(new AlterColumnOperation
        {
            Table = "payment_links",
            Name = "terms_url",
            ColumnType = "text",
            IsNullable = false,
            OldColumn = new AddColumnOperation { Table = "payment_links", Name = "terms_url", ColumnType = "text", IsNullable = true },
        });

        sql.ShouldNotContain("TYPE");
        sql.ShouldContain("ALTER COLUMN terms_url SET NOT NULL");
    }

    [Fact]
    public void Generate_WhenAlterColumnOnlyChangesNullabilityWithStockGenerator_ThenAltersType()
    {
        // The bug the designerless generator exists for: without a model EF ignores OldColumn.
        var sql = GenerateSql(
            new AlterColumnOperation
            {
                Table = "payment_links",
                Name = "terms_url",
                ColumnType = "text",
                IsNullable = false,
                OldColumn = new AddColumnOperation { Table = "payment_links", Name = "terms_url", ColumnType = "text", IsNullable = true },
            },
            designerless: false);

        sql.ShouldContain("ALTER COLUMN terms_url TYPE text");
    }

    [Fact]
    public void Generate_WhenAlterColumnOnlyChangesDefault_ThenDoesNotAlterType()
    {
        var sql = GenerateSql(new AlterColumnOperation
        {
            Table = "payment_links",
            Name = "status",
            ColumnType = "integer",
            IsNullable = false,
            DefaultValue = 1,
            OldColumn = new AddColumnOperation { Table = "payment_links", Name = "status", ColumnType = "integer", IsNullable = false },
        });

        sql.ShouldNotContain("TYPE");
        sql.ShouldContain("ALTER COLUMN status SET DEFAULT 1");
    }

    [Fact]
    public void Generate_WhenAlterColumnChangesType_ThenAltersType()
    {
        var sql = GenerateSql(new AlterColumnOperation
        {
            Table = "payment_links",
            Name = "amount",
            ColumnType = "bigint",
            IsNullable = false,
            OldColumn = new AddColumnOperation { Table = "payment_links", Name = "amount", ColumnType = "integer", IsNullable = false },
        });

        sql.ShouldContain("ALTER COLUMN amount TYPE bigint");
    }

    [Fact]
    public void Generate_WhenDataOperationHasColumnTypes_ThenGeneratesSql()
    {
        var sql = GenerateSql(new InsertDataOperation
        {
            Table = "currencies",
            Columns = ["code", "decimals"],
            ColumnTypes = ["text", "integer"],
            Values = new object[,] { { "EUR", 2 } },
        });

        sql.ShouldContain("INSERT INTO currencies (code, decimals)");
        sql.ShouldContain("VALUES ('EUR', 2)");
    }

    [Fact]
    public void Generate_WhenDataOperationLacksColumnTypes_ThenThrows()
    {
        // Why MigrationConventions requires column types: without a model EF fails when the migration is applied.
        var exception = Should.Throw<InvalidOperationException>(() => GenerateSql(new InsertDataOperation
        {
            Table = "currencies",
            Columns = ["code", "decimals"],
            Values = new object[,] { { "EUR", 2 } },
        }));

        exception.Message.ShouldContain("specify the column types");
    }

    private static string GenerateSql(MigrationOperation operation, bool designerless = true)
    {
        var options = new DbContextOptionsBuilder().UseNpgsql("Host=localhost;Database=unused");

        if (designerless)
        {
            options.UseDesignerlessMigrations();
        }

        using var context = new DbContext(options.Options);
        var commands = context.GetService<IMigrationsSqlGenerator>().Generate([operation], model: null);

        return string.Join(Environment.NewLine, commands.Select(x => x.CommandText));
    }
}
