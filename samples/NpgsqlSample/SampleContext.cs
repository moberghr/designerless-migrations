using Microsoft.EntityFrameworkCore;

namespace NpgsqlSample;

public class Blog
{
    public int Id { get; set; }

    public required string Url { get; set; }

    public required string Name { get; set; }
}

public class Currency
{
    public required string Code { get; set; }

    public int Decimals { get; set; }
}

public partial class SampleContext(DbContextOptions<SampleContext> options) : DbContext(options)
{
    public DbSet<Blog> Blogs { get; set; } = null!;

    public DbSet<Currency> Currencies { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Currency>().HasKey(x => x.Code);
    }
}
