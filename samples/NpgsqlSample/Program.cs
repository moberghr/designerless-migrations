using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NpgsqlSample;

var builder = Host.CreateApplicationBuilder(args);

// The whole setup: UseDesignerlessMigrations() after UseNpgsql(). dotnet ef picks up the scaffolder by itself.
builder.Services.AddDbContext<SampleContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Sample") ?? "Host=localhost;Database=npgsql_sample")
    .UseDesignerlessMigrations());

// dotnet ef stops the app here and takes the configured context from the host.
using var host = builder.Build();

switch (args.FirstOrDefault())
{
    case "migrate":
        using (var scope = host.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<SampleContext>().Database.Migrate();
        }

        Console.WriteLine("Migrated.");
        break;

    case "check-types":
        // Loads every type in this assembly, which fails if any of them depends on a design-time-only assembly.
        Console.WriteLine($"Loaded {typeof(Program).Assembly.GetTypes().Length} types.");
        break;

    default:
        Console.WriteLine("Usage: NpgsqlSample migrate | check-types");
        break;
}
