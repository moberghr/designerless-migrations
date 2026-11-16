using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlSample;

#nullable disable

namespace NpgsqlSample.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(SampleContext))]
    [Migration("20261116122518_RequireBlogName")]
    public partial class RequireBlogName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Blogs",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
