using CrestApps.OrchardCore.SigningKeys;
using CrestApps.OrchardCore.SigningKeys.Core.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the shared signing keys.
/// </summary>
public static class SigningKeysCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the key ring and its daily rotation task, once however many features call it. The feature must depend
    /// on Orchard Core Secrets (<c>OrchardCore.Secrets</c>), which keeps the key pairs, and register its key sets with
    /// <c>AddSigningKeySet</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSigningKeys(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<SigningKeyOptions>();
        services.TryAddScoped<ISigningKeyRing, SigningKeyRing>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, SigningKeyRotationBackgroundTask>());

        return services;
    }
}
