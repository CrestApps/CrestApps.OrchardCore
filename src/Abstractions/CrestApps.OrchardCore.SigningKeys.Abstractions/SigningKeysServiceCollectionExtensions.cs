using CrestApps.OrchardCore.SigningKeys;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers key sets of the shared signing keys.
/// </summary>
public static class SigningKeysServiceCollectionExtensions
{
    /// <summary>
    /// Registers a key set. The feature also calls <c>AddSigningKeys()</c> of Signing Keys Core, for the ring and the
    /// rotation task, and depends on Orchard Core Secrets, which keeps the key pairs.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The stable name of the key set.</param>
    /// <param name="configure">Configures the key set.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSigningKeySet(this IServiceCollection services, string name, Action<SigningKeySetOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);

        services.Configure<SigningKeyOptions>(options =>
        {
            var keySet = options.GetOrAdd(name);

            configure?.Invoke(keySet);
        });

        return services;
    }
}
