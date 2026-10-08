using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Core;

/// <summary>
/// Builds the choices of a caller ID field from the address list, so the number shown to the people called is one the
/// business owns and uses for calls, picked rather than typed.
/// </summary>
public static class OmnichannelCallerIdOptionsExtensions
{
    /// <summary>
    /// Gets the numbers used for voice calls as caller ID choices, each valued with its number. A stored number that is
    /// not among them, typed before caller IDs were picked or since removed from calls, is kept as a choice so that saving
    /// the screen does not change it.
    /// </summary>
    /// <param name="addressManager">The address list.</param>
    /// <param name="selectedNumber">The number currently stored, if any.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The choices, ordered by name, with the stored number selected.</returns>
    public static async Task<IList<SelectListItem>> GetCallerIdOptionsAsync(
        this IOmnichannelChannelEndpointManager addressManager,
        string selectedNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addressManager);

        var selected = selectedNumber?.Trim();
        var options = (await addressManager.GetAllAsync(cancellationToken))
            .Where(address => address.HasCapability(OmnichannelConstants.Channels.Phone) && !string.IsNullOrWhiteSpace(address.Value))
            .OrderBy(address => address.DisplayText ?? address.Value, StringComparer.CurrentCultureIgnoreCase)
            .Select(address => new SelectListItem(
                Describe(address),
                address.Value,
                string.Equals(address.Value, selected, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (!string.IsNullOrEmpty(selected) && !options.Any(option => option.Selected))
        {
            options.Insert(0, new SelectListItem(selected, selected, selected: true));
        }

        return options;
    }

    private static string Describe(OmnichannelChannelEndpoint address)
        => string.IsNullOrWhiteSpace(address.DisplayText) || address.DisplayText == address.Value
            ? address.Value
            : $"{address.DisplayText} ({address.Value})";
}
