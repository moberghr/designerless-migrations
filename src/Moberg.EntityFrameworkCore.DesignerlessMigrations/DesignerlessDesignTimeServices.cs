using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations;

/// <summary>
/// Replaces EF's <see cref="IMigrationsScaffolder"/> with <see cref="DesignerlessMigrationsScaffolder"/>.
/// <para>
/// Registered automatically: the package's build props add <c>[assembly: DesignTimeServicesReference]</c> pointing here
/// to the startup project, and <c>dotnet ef</c> reads it from there. EF applies referenced services first; the provider's
/// and EF's own defaults are only added if missing, and the startup project's own <see cref="IDesignTimeServices"/> runs
/// last, so a project can still override this.
/// </para>
/// </summary>
public class DesignerlessDesignTimeServices : IDesignTimeServices
{
    /// <inheritdoc />
    public void ConfigureDesignTimeServices(IServiceCollection serviceCollection)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);

        serviceCollection.AddScoped<IMigrationsScaffolder, DesignerlessMigrationsScaffolder>();
    }
}
