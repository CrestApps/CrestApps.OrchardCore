using CrestApps.OrchardCore.Learning.Credentials.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the extension points of Digital Credentials.
/// </summary>
public static class CredentialsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a kind of award rule, so the definition editor offers it.
    /// </summary>
    /// <typeparam name="T">The rule kind.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddCredentialRuleKind<T>(this IServiceCollection services)
        where T : class, ICredentialRuleKind
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICredentialRuleKind, T>());

        return services;
    }

    /// <summary>
    /// Registers an observer of credential awards.
    /// </summary>
    /// <typeparam name="T">The observer.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddCredentialAwardObserver<T>(this IServiceCollection services)
        where T : class, ICredentialAwardObserver
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICredentialAwardObserver, T>());

        return services;
    }
}
