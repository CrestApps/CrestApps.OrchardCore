using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Reads the agents who send from a messaging number, stored as <see cref="MessagingLineSettings"/>.
/// </summary>
public static class MessagingLines
{
    /// <summary>
    /// Finds the number on a channel whose messaging line names the user.
    /// </summary>
    /// <param name="addresses">The addresses to look in.</param>
    /// <param name="userId">The user.</param>
    /// <param name="channel">The messaging channel, such as SMS.</param>
    /// <param name="excludeAddressId">An address to leave out, such as the one being saved.</param>
    /// <returns>The oldest such number, or <see langword="null"/>.</returns>
    public static OmnichannelChannelEndpoint FindAssignedLine(IEnumerable<OmnichannelChannelEndpoint> addresses, string userId, string channel, string excludeAddressId = null)
    {
        if (addresses is null || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(channel))
        {
            return null;
        }

        return addresses
            .Where(address => address.HasCapability(channel) &&
                !string.IsNullOrWhiteSpace(address.Value) &&
                !string.Equals(address.ItemId, excludeAddressId, StringComparison.Ordinal) &&
                GetUserIds(address).Contains(userId, StringComparer.Ordinal))
            .OrderBy(address => address.CreatedUtc)
            .ThenBy(address => address.ItemId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets the users who send from a number.
    /// </summary>
    /// <param name="address">The number.</param>
    /// <returns>The user identifiers.</returns>
    public static IReadOnlyList<string> GetUserIds(OmnichannelChannelEndpoint address)
    {
        if (address is null || !address.TryGet<MessagingLineSettings>(out var settings) || settings?.UserIds is null)
        {
            return [];
        }

        return settings.UserIds
            .Where(userId => !string.IsNullOrWhiteSpace(userId))
            .ToArray();
    }
}
