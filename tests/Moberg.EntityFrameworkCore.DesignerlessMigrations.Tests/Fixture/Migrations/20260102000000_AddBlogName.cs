using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests.Fixture.Migrations;

[DbContext(typeof(BloggingContext))]
[Migration("20260102000000_AddBlogName")]
public class AddBlogName : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Name", table: "Blogs", type: "text", nullable: true);
    }
}
