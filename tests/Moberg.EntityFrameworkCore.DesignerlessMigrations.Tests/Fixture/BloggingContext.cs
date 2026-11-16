using Microsoft.EntityFrameworkCore;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.Tests.Fixture;

public class Blog
{
    public int Id { get; set; }

    public string? Name { get; set; }
}

/// <summary>A small context whose migrations and snapshot live in this assembly, as in a consumer project.</summary>
public class BloggingContext(DbContextOptions<BloggingContext> options) : DbContext(options)
{
    public DbSet<Blog> Blogs => Set<Blog>();

    public static BloggingContext Create() =>
        new(new DbContextOptionsBuilder<BloggingContext>().UseNpgsql("Host=localhost;Database=unused").Options);
}
