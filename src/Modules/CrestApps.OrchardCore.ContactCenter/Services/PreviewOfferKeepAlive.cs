using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Keeps a preview offer with the agent while the activity screen it opened is still in front of them.
/// </summary>
/// <remarks>
/// A preview offer is pending until the agent dials, and it used to run out while they reviewed the record, updated the
/// contact and chose a disposition. Each time the screen says the agent is still there the offer is pushed out by the
/// dialer profile's offer time again. Only preview records are kept: an inbound caller is waiting on the line, and an
/// offer that keeps ringing an absent agent would keep them waiting.
/// </remarks>
internal sealed class PreviewOfferKeepAlive : IActivityOfferKeepAlive
{
    private readonly IActivityReservationService _reservationService;
    private readonly IDialerProfileReader _dialerProfileReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewOfferKeepAlive"/> class.
    /// </summary>
    /// <param name="reservationService">The reservation service that extends the offer.</param>
    /// <param name="dialerProfileReader">The reader for the profile that says how long the offer lasts.</param>
    public PreviewOfferKeepAlive(
        IActivityReservationService reservationService,
        IDialerProfileReader dialerProfileReader)
    {
        _reservationService = reservationService;
        _dialerProfileReader = dialerProfileReader;
    }

    /// <inheritdoc/>
    public async Task<DateTime?> KeepAliveAsync(OmnichannelActivity activity, string userId, CancellationToken cancellationToken = default)
    {
        if (activity is null ||
            string.IsNullOrEmpty(activity.ItemId) ||
            string.IsNullOrEmpty(userId) ||
            !string.Equals(activity.Source, ActivitySources.PreviewDial, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var seconds = DialerProfile.DefaultPreviewOfferTimeoutSeconds;

        if (!string.IsNullOrEmpty(activity.DialerProfileId))
        {
            var profile = await _dialerProfileReader.FindByIdAsync(activity.DialerProfileId, cancellationToken);

            if (profile?.PreviewOfferTimeoutSeconds > 0)
            {
                seconds = profile.PreviewOfferTimeoutSeconds;
            }
        }

        return await _reservationService.ExtendPendingAsync(activity.ItemId, userId, seconds, cancellationToken);
    }
}
