using Microsoft.EntityFrameworkCore;

namespace ConventionFixtures;

public class ConventionsContext(DbContextOptions<ConventionsContext> options) : DbContext(options)
{
    public static ConventionsContext Create() =>
        new(new DbContextOptionsBuilder<ConventionsContext>().UseNpgsql("Host=localhost;Database=unused").Options);
}

public class OtherContext(DbContextOptions<OtherContext> options) : DbContext(options);
