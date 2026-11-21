using CrestApps.OrchardCore.SigningKeys;
using CrestApps.OrchardCore.SigningKeys.Core.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the shared signing keys.
/// </summary>
public static class SigningKeysCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the key ring, its daily rotation task and the tenant event that makes each set's first key when the
    /// tenant starts, once however many features call it. The feature must depend on Orchard Core Secrets
    /// (<c>OrchardCore.Secrets</c>), which keeps the key pairs, and register its key sets with <c>AddSigningKeySet</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSigningKeys(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<SigningKeyOptions>();
        services.TryAddScoped<ISigningKeyRing, SigningKeyRing>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, SigningKeyRotationBackgroundTask>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IModularTenantEvents, SigningKeysTenantEvents>());

        return services;
    }
}
