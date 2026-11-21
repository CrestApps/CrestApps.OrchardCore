using CrestApps.OrchardCore.Learning.Packages.Core.Cmi5;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Xapi;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers cmi5 packages.
/// </summary>
public static class LearningCmi5CoreServiceCollectionExtensions
{
    /// <summary>
    /// Lets the importer take cmi5 packages and registers the cmi5 launch service, the progress and the statement filter
    /// that enforces the cmi5 rules in the LRS. The Learning Record Store core must be registered too.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLearningCmi5Core(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Configure<LearningPackageOptions>(options => options.AcceptCmi5 = true);
        services.TryAddScoped<Cmi5LaunchService>();
        services.TryAddScoped<Cmi5Progress>();
        services.AddXapiStatementFilter<Cmi5StatementFilter>();

        return services;
    }
}
