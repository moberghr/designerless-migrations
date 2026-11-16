using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests.Fixture.Migrations;

[DbContext(typeof(BloggingContext))]
[Migration("20260101000000_Initial")]
public class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Blogs",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            },
            constraints: table => table.PrimaryKey("PK_Blogs", x => x.Id));
    }
}
