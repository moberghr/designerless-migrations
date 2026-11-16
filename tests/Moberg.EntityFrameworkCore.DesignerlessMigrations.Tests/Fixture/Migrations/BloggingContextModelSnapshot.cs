using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests.Fixture.Migrations;

[DbContext(typeof(BloggingContext))]
public class BloggingContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.15")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests.Fixture.Blog", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("integer");

            NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

            b.Property<string>("Name")
                .HasColumnType("text");

            b.HasKey("Id");

            b.ToTable("Blogs");
        });
    }
}
