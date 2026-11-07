using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Core.Migrations;

/// <summary>
/// Registers the log of migration skips.
/// </summary>
public static class MigrationSkipLogServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IMigrationSkipLog"/>, so <see cref="MigrationSkipReport.LogSummaryAsync"/> keeps each skip
    /// summary for administrators. Calling it more than once is safe.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMigrationSkipLog(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IMigrationSkipLog, MigrationSkipLog>();

        return services;
    }
}
