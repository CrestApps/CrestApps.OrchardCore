using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the host-level process liveness probe.
/// </summary>
public static class ContactCenterProcessHealthServiceCollectionExtensions
{
    /// <summary>
    /// Registers the process liveness probe on its default path.
    /// </summary>
    /// <param name="services">The service collection to add the registrations to.</param>
    /// <returns>The same <paramref name="services"/> so calls can be chained.</returns>
    public static IServiceCollection AddContactCenterProcessLiveness(this IServiceCollection services)
        => services.AddContactCenterProcessLiveness(ContactCenterConstants.HealthChecks.ProcessLivenessPath);

    /// <summary>
    /// Registers the process liveness probe on a custom path.
    /// </summary>
    /// <param name="services">The service collection to add the registrations to.</param>
    /// <param name="path">The path the probe should answer on.</param>
    /// <returns>The same <paramref name="services"/> so calls can be chained.</returns>
    /// <remarks>
    /// This also registers a startup validator that fails the host when any configured tenant maps the shared
    /// health-check endpoint on the same path. The probe short-circuits before routing, so such a collision
    /// would otherwise replace that tenant's health endpoint with an unconditional success rather than produce
    /// a routing error.
    /// </remarks>
    public static IServiceCollection AddContactCenterProcessLiveness(this IServiceCollection services, string path)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var existing = services.FirstOrDefault(descriptor =>
            descriptor.ServiceType == typeof(ContactCenterProcessLivenessOptions));

        if (existing?.ImplementationInstance is ContactCenterProcessLivenessOptions registered)
        {
            if (!registered.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The Contact Center process liveness probe is already registered at '{registered.Path}' and " +
                    $"cannot also be registered at '{path}'. The probe answers on exactly one path, and only the " +
                    "last registration would take effect while every earlier one was silently discarded. Register " +
                    "it once.");
            }

            return services;
        }

        // Registered as a concrete singleton rather than through IOptions, because IOptions always resolves to
        // a default instance and could not tell the middleware whether this method had been called at all.
        services.AddSingleton(new ContactCenterProcessLivenessOptions { Path = path });

        // The shell settings manager is resolved leniently so the probe can also be hosted outside an Orchard
        // Core application, where there are no tenants to validate.
        services.AddSingleton<IHostedService>(serviceProvider => new ContactCenterProcessLivenessPathValidator(
            serviceProvider.GetService<IShellSettingsManager>(),
            serviceProvider.GetRequiredService<ContactCenterProcessLivenessOptions>()));

        return services;
    }
}
