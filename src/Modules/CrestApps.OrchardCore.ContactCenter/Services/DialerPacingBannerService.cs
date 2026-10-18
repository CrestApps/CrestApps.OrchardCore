using System.Globalization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Provides the default implementation of <see cref="IDialerPacingBannerService"/>.
/// </summary>
public sealed class DialerPacingBannerService : IDialerPacingBannerService
{
    /// <summary>
    /// The most campaigns a banner names before it says how many more there are.
    /// </summary>
    internal const int MaxNamedCampaigns = 3;

    private readonly IDialerPacingHealthService _healthService;
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerPacingBannerService"/> class.
    /// </summary>
    /// <param name="healthService">The service that reads what the dialer is doing for each paced campaign queue.</param>
    /// <param name="campaignManagers">The campaign catalog, when the campaign feature is on, for campaign names.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialerPacingBannerService(
        IDialerPacingHealthService healthService,
        IEnumerable<ICatalogManager<OmnichannelCampaign>> campaignManagers,
        IStringLocalizer<DialerPacingBannerService> stringLocalizer)
    {
        _healthService = healthService;
        _campaignManager = campaignManagers.FirstOrDefault();
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<DialerPacingBanner> GetAsync(Func<string, Task<bool>> canSeeQueue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canSeeQueue);

        var snapshot = await _healthService.EvaluateAsync(cancellationToken);
        var visible = new List<DialerPacingQueueHealth>();

        foreach (var queue in snapshot.Queues)
        {
            if (await canSeeQueue(queue.QueueId))
            {
                visible.Add(queue);
            }
        }

        snapshot.Queues = visible;

        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        if (_campaignManager is not null)
        {
            foreach (var campaignId in visible.Select(queue => queue.CampaignId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
            {
                var campaign = await _campaignManager.FindByIdAsync(campaignId, cancellationToken);

                if (!string.IsNullOrWhiteSpace(campaign?.DisplayText))
                {
                    names[campaignId] = campaign.DisplayText;
                }
            }
        }

        return Build(snapshot, S, names);
    }

    /// <summary>
    /// Builds the banner for a dialer state.
    /// </summary>
    /// <param name="snapshot">The dialer's state over the queues the viewer may see.</param>
    /// <param name="S">The localizer.</param>
    /// <param name="campaignNames">The names of the campaigns, by identifier.</param>
    /// <returns>The banner.</returns>
    internal static DialerPacingBanner Build(
        DialerPacingHealthSnapshot snapshot,
        IStringLocalizer S,
        IReadOnlyDictionary<string, string> campaignNames)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(S);

        var state = snapshot.State;
        var inState = snapshot.Queues.Where(queue => queue.State == state).ToArray();
        var campaigns = Name(inState, S, campaignNames);

        // The queue whose lock has been out of reach longest says how long the dialer has not paced.
        var lockNotAcquiredSinceUtc = inState.Min(queue => queue.LockNotAcquiredSinceUtc) ?? snapshot.EvaluatedUtc;

        return state switch
        {
            DialerPacingHealthState.LockNotAcquired => new DialerPacingBanner
            {
                State = state,
                Level = "danger",
                Message = S["Dialer not pacing: the pacing lock of {0} has not been acquired since {1} ({2} minutes ago), so every pacing cycle skips itself. Check the distributed lock.",
                    campaigns,
                    lockNotAcquiredSinceUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture),
                    (int)(snapshot.EvaluatedUtc - lockNotAcquiredSinceUtc).TotalMinutes].Value,
            },
            DialerPacingHealthState.Stalled => new DialerPacingBanner
            {
                State = state,
                Level = "danger",
                Message = S["Dialer stalled: due work is waiting but no pacing cycle has completed for {0} minutes ({1}).",
                    (int)inState.Max(queue => queue.TimeWithoutCompletedCycle).TotalMinutes,
                    campaigns].Value,
            },
            DialerPacingHealthState.Idle => new DialerPacingBanner
            {
                State = state,
                Level = "warning",
                Message = S["Dialer idle: work is due ({0}) but no agent is available to take it.", campaigns].Value,
            },
            DialerPacingHealthState.HeldBack => new DialerPacingBanner
            {
                State = state,
                Level = "warning",
                Message = S["Dialer holding back ({0}): the abandonment policy, the compliance check or calls already in flight keep it from dialing.", campaigns].Value,
            },
            DialerPacingHealthState.Starting => new DialerPacingBanner
            {
                State = state,
                Level = "info",
                Message = S["Dialer starting: work arrived ({0}) and its first pacing cycle has not completed yet.", campaigns].Value,
            },
            DialerPacingHealthState.Dialing => new DialerPacingBanner
            {
                State = state,
                Level = "success",
                Message = S["Dialer active: placing calls ({0}).", campaigns].Value,
            },
            // Records waiting in a paced queue are never "nothing to call": the cycles found none due yet, which is a
            // different thing to tell a supervisor looking at a loaded campaign.
            _ when inState.Length > 0 => new DialerPacingBanner
            {
                State = DialerPacingHealthState.NothingToCall,
                Level = "secondary",
                Message = S["Nothing due yet: records wait ({0}), but the latest pacing cycle found none it may dial now.", campaigns].Value,
            },
            _ => new DialerPacingBanner
            {
                State = DialerPacingHealthState.NothingToCall,
                Level = "secondary",
                Message = S["Nothing to call: no paced campaign has work waiting."].Value,
            },
        };
    }

    private static string Name(
        IReadOnlyCollection<DialerPacingQueueHealth> queues,
        IStringLocalizer S,
        IReadOnlyDictionary<string, string> campaignNames)
    {
        var names = queues
            .Select(queue => campaignNames is not null && !string.IsNullOrEmpty(queue.CampaignId) && campaignNames.TryGetValue(queue.CampaignId, out var name)
                ? name
                : queue.CampaignId ?? queue.QueueId)
            .ToArray();

        if (names.Length <= MaxNamedCampaigns)
        {
            return string.Join(", ", names);
        }

        return S["{0} and {1} more", string.Join(", ", names.Take(MaxNamedCampaigns)), names.Length - MaxNamedCampaigns].Value;
    }
}
