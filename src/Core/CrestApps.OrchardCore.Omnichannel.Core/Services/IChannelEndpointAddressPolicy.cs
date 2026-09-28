using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Says how the address of a channel endpoint is stored and what counts as a valid one, for channels contributed by
/// other features. The channel-endpoint handler canonicalizes and validates phone, SMS and email endpoints itself; for
/// any other channel it asks the registered policies, so a feature that adds a channel (a messaging channel such as
/// WhatsApp) makes its endpoints match their inbound traffic without registering a second endpoint handler.
/// </summary>
public interface IChannelEndpointAddressPolicy
{
    /// <summary>
    /// Determines whether this policy governs the endpoints of a channel.
    /// </summary>
    /// <param name="channel">The endpoint's channel.</param>
    /// <returns><see langword="true"/> when this policy governs the channel.</returns>
    bool AppliesTo(string channel);

    /// <summary>
    /// Brings an endpoint address into the form the channel stores and matches inbound traffic on.
    /// </summary>
    /// <param name="channel">The endpoint's channel.</param>
    /// <param name="address">The address as entered or imported.</param>
    /// <returns>The normalized address.</returns>
    string Normalize(string channel, string address);

    /// <summary>
    /// Validates an endpoint address.
    /// </summary>
    /// <param name="channel">The endpoint's channel.</param>
    /// <param name="address">The address to validate.</param>
    /// <returns>The error to show, or <see langword="null"/> when the address is valid.</returns>
    LocalizedString Validate(string channel, string address);
}
