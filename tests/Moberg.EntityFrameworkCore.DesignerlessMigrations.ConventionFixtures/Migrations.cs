using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ConventionFixtures;

[DbContext(typeof(ConventionsContext))]
[Migration("20260101000000_Clean")]
public class Clean : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            table: "currencies",
            columns: ["code", "decimals"],
            columnTypes: ["text", "integer"],
            values: new object[] { "EUR", 2 });
        migrationBuilder.UpdateData(
            table: "currencies",
            keyColumns: ["code"],
            keyColumnTypes: ["text"],
            keyValues: ["JPY"],
            columns: ["decimals"],
            columnTypes: ["integer"],
            values: [0]);
        migrationBuilder.DeleteData(
            table: "currencies",
            keyColumns: ["code"],
            keyColumnTypes: ["text"],
            keyValues: ["XXX"]);
    }
}

[DbContext(typeof(ConventionsContext))]
[Migration("20260102000000_WithDown")]
public class WithDown : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("SELECT 1");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("SELECT 1");
}

[DbContext(typeof(ConventionsContext))]
[Migration("20260103000000_WithDesigner")]
public partial class WithDesigner : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("SELECT 1");
}

// What a leftover .Designer.cs contributes.
public partial class WithDesigner
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
    }
}

// No attributes at all: EF never sees it.
public class Unattributed : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("SELECT 1");
}

// [DbContext] alone is not enough: EF also needs [Migration].
[DbContext(typeof(ConventionsContext))]
public class OnlyDbContextAttributed : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("SELECT 1");
}

[DbContext(typeof(ConventionsContext))]
[Migration("20260106000000_DataWithoutTypes")]
public class DataWithoutTypes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(table: "currencies", columns: ["code", "decimals"], values: new object[] { "EUR", 2 });
        migrationBuilder.UpdateData(table: "currencies", keyColumn: "code", keyValue: "JPY", column: "decimals", value: 0);
        migrationBuilder.DeleteData(table: "currencies", keyColumn: "code", keyValue: "XXX");
    }
}

[DbContext(typeof(ConventionsContext))]
[Migration("20260107000000_DownDataWithoutTypes")]
public class DownDataWithoutTypes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("SELECT 1");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DeleteData(table: "currencies", keyColumn: "code", keyValue: "XXX");
}

// Belongs to another context: none of ConventionsContext's business, even though it declares Down().
[DbContext(typeof(OtherContext))]
[Migration("20260108000000_OtherContextMigration")]
public class OtherContextMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("SELECT 1");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("SELECT 1");
}

// Abstract bases are not migrations.
public abstract class MigrationBase : Migration
{
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("SELECT 1");
}
