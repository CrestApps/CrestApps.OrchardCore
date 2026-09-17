using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers which host deployment units own which Contact Center capabilities.
/// </summary>
public static class ContactCenterFeatureCapabilityServiceCollectionExtensions
{
    /// <summary>
    /// Declares that a feature owns a capability, so disabling the feature drains that capability's work.
    /// </summary>
    /// <remarks>
    /// Called from the startup that owns the participant. A provider module names its own feature id
    /// and the capability it contributes to, which is why this lives in the abstractions package that
    /// every provider already references.
    /// </remarks>
    /// <param name="services">The services.</param>
    /// <param name="featureId">The feature that owns the capability.</param>
    /// <param name="capability">The capability. See <see cref="ContactCenterCapabilities"/>.</param>
    public static IServiceCollection AddContactCenterCapability(this IServiceCollection services, string featureId, string capability)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new ContactCenterFeatureCapabilityMapping(featureId, capability));

        return services;
    }
}
