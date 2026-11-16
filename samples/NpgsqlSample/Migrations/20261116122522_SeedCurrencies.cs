using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlSample;

#nullable disable

namespace NpgsqlSample.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(SampleContext))]
    [Migration("20261116122522_SeedCurrencies")]
    public partial class SeedCurrencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Without a Designer model EF can't look column types up, so data operations pass them explicitly.
            migrationBuilder.InsertData(
                table: "Currencies",
                columns: ["Code", "Decimals"],
                columnTypes: ["text", "integer"],
                values: new object[,]
                {
                    { "EUR", 2 },
                    { "JPY", 0 },
                });
        }
    }
}
