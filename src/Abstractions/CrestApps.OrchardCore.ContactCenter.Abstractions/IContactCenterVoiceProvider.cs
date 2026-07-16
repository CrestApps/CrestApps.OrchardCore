using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Identifies a Contact Center voice provider and the executable capability contracts it advertises.
/// </summary>
public interface IContactCenterVoiceProvider
{
    /// <summary>
    /// Gets the stable technical name used to resolve the provider.
    /// </summary>
    string TechnicalName { get; }

    /// <summary>
    /// Gets the localized, human-readable name of the provider.
    /// </summary>
    LocalizedString Name { get; }

    /// <summary>
    /// Gets the provider capabilities supported for Contact Center orchestration.
    /// </summary>
    ContactCenterVoiceProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Gets the delivery model that describes how the provider delivers a live call to an agent.
    /// </summary>
    VoiceProviderDeliveryModel DeliveryModel { get; }
}
