using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Users;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Moves an activity between the dialer and an agent, or to another campaign or dialer profile, so a workflow decides
/// who makes the next call. A finished activity's open follow-ups are moved instead, and when it has none its next
/// attempt can be created already moved.
/// </summary>
/// <remarks>
/// A disposition's "Try again" queues the next attempt of a dialer record in the same campaign, under the same dialer
/// profile, so the dialer calls it again. A workflow had no way to say the next call is an agent's, or another
/// campaign's: it could only ask the dialer to try again. An activity moved to an agent is taken out of the dialer
/// queue here, and routing drops any dialer record whose activity is no longer dialer work, so the dialer does not
/// call it again even when the next attempt is queued a moment after the move.
/// </remarks>
public sealed class ActivityRerouter
{
    // The activities a reroute changes: not started, so nobody is on a call for them.
    private static readonly ActivityStatus[] _openStatuses =
    [
        ActivityStatus.NotStated,
        ActivityStatus.Scheduled,
        ActivityStatus.Pending,
        ActivityStatus.AwaitingAgentResponse,
    ];

    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IActivityDialerContributor _dialerContributor;
    private readonly ISession _session;
    private readonly IContentManager _contentManager;
    private readonly IContactMethodsAccessor _contactMethods;
    private readonly UserManager<IUser> _userManager;
    private readonly ActivitySourceOptions _sourceOptions;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityRerouter"/> class.
    /// </summary>
    public ActivityRerouter(
        IOmnichannelActivityManager activityManager,
        IEnumerable<IActivityDialerContributor> dialerContributors,
        ISession session,
        IContentManager contentManager,
        IContactMethodsAccessor contactMethods,
        UserManager<IUser> userManager,
        IOptions<ActivitySourceOptions> sourceOptions,
        IClock clock,
        ILogger<ActivityRerouter> logger)
    {
        _activityManager = activityManager;
        _dialerContributor = dialerContributors.FirstOrDefault();
        _session = session;
        _contentManager = contentManager;
        _contactMethods = contactMethods;
        _userManager = userManager;
        _sourceOptions = sourceOptions.Value;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Reroutes an activity: an open one is moved itself; a finished one has its open follow-ups moved, or its next
    /// attempt created already moved when it has none and <see cref="ActivityRerouteRequest.CreateNextAttempt"/> is set.
    /// </summary>
    /// <param name="activityItemId">The activity to reroute.</param>
    /// <param name="request">What the activity is rerouted to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The activities that were rerouted or created.</returns>
    public async Task<ActivityRerouteResult> RerouteAsync(string activityItemId, ActivityRerouteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(activityItemId))
        {
            return ActivityRerouteResult.Failed("No activity was named.");
        }

        var activity = await _activityManager.FindByIdAsync(activityItemId.Trim(), cancellationToken);

        if (activity is null)
        {
            return ActivityRerouteResult.Failed("The activity does not exist.");
        }

        var route = await ResolveRouteAsync(request, cancellationToken);

        if (route.Error is not null)
        {
            return ActivityRerouteResult.Failed(route.Error);
        }

        var result = new ActivityRerouteResult();

        if (!activity.Status.IsTerminal())
        {
            await RerouteExistingAsync([activity.ItemId], route, result, cancellationToken);

            return Conclude(result, activity);
        }

        var followUpIds = await FindOpenFollowUpIdsAsync(activity, cancellationToken);

        if (followUpIds.Count > 0)
        {
            await RerouteExistingAsync(followUpIds, route, result, cancellationToken);

            return Conclude(result, activity);
        }

        if (!request.CreateNextAttempt)
        {
            result.Status = ActivityRerouteStatus.NothingToReroute;
            result.Reason = "The activity is finished and has no open follow-up.";

            return result;
        }

        await CreateNextAttemptAsync(activity, route, request.DelayMinutes, result, cancellationToken);

        return Conclude(result, activity);
    }

    /// <summary>
    /// Whether an activity an agent works may carry the source: not the AI's, and not a dialer's, which a dialer
    /// profile sets.
    /// </summary>
    internal static bool IsAgentSource(string source)
        => !string.IsNullOrWhiteSpace(source) &&
            !ActivitySources.IsDialer(source) &&
            !string.Equals(source, ActivitySources.Automatic, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Moves an activity to an agent: off the dialer, with a source the dialer does not drive, owned by the named user
    /// or by the owner it already has.
    /// </summary>
    internal static void ApplyAgentRoute(OmnichannelActivity activity, string source, string campaignId, string ownerId, string ownerName, DateTime utcNow)
    {
        activity.Source = source;
        activity.CampaignId = campaignId;
        activity.DialerProfileId = null;
        activity.InteractionType = ActivityInteractionType.Manual;
        activity.AISessionId = null;
        ClearReservation(activity);

        if (!string.IsNullOrEmpty(ownerId))
        {
            activity.AssignedToId = ownerId;
            activity.AssignedToUsername = ownerName;
            activity.AssignedToUtc = utcNow;
            activity.AssignmentStatus = ActivityAssignmentStatus.Assigned;
        }
        else if (string.IsNullOrEmpty(activity.AssignedToId))
        {
            activity.AssignmentStatus = ActivityAssignmentStatus.Available;
        }

        ApplyInitialStatus(activity);
    }

    /// <summary>
    /// Moves an activity to the dialer: a phone call on the campaign, under the profile, offered by the dialer to
    /// whichever agent signed in to the campaign is free, so it has no owner of its own.
    /// </summary>
    internal static void ApplyDialerRoute(OmnichannelActivity activity, ActivityDialerProfileDescriptor profile, string campaignId, string destination)
    {
        activity.CampaignId = campaignId;
        activity.Source = profile.ActivitySource;
        activity.DialerProfileId = profile.ProfileId;
        activity.InteractionType = ActivityInteractionType.Manual;
        activity.Channel = OmnichannelConstants.Channels.Phone;
        activity.Kind = ActivityKind.Call;
        activity.PreferredDestination = destination;
        activity.AISessionId = null;
        activity.AssignedToId = null;
        activity.AssignedToUsername = null;
        activity.AssignedToUtc = null;
        activity.AssignmentStatus = ActivityAssignmentStatus.Available;
        ClearReservation(activity);
        ApplyInitialStatus(activity);
    }

    private static ActivityRerouteResult Conclude(ActivityRerouteResult result, OmnichannelActivity activity)
    {
        result.Status = result.ActivityItemIds.Count > 0 ? ActivityRerouteStatus.Rerouted : ActivityRerouteStatus.Failed;
        result.Reason ??= result.Status == ActivityRerouteStatus.Failed
            ? $"No activity of '{activity.ItemId}' could be rerouted."
            : null;

        return result;
    }

    private async Task<ResolvedRoute> ResolveRouteAsync(ActivityRerouteRequest request, CancellationToken cancellationToken)
    {
        var route = new ResolvedRoute
        {
            Target = request.Target,
            CampaignId = Normalize(request.CampaignId),
        };

        switch (request.Target)
        {
            case ActivityRerouteTarget.Dialer:
                if (_dialerContributor is null)
                {
                    return route.Fail("The dialer is not enabled.");
                }

                var profileId = Normalize(request.DialerProfileId);

                if (profileId is null)
                {
                    return route.Fail("No dialer profile was named.");
                }

                route.Profile = await _dialerContributor.FindByIdAsync(profileId, cancellationToken);

                if (route.Profile is null)
                {
                    return route.Fail($"The dialer profile '{profileId}' does not exist.");
                }

                break;

            case ActivityRerouteTarget.Agent:
                var source = Normalize(request.Source) ?? ActivitySources.Manual;

                // Only a source no other feature owns: a dialer mode is set through a dialer profile.
                if (!_sourceOptions.TryGetManuallyAssignableSource(source, out var entry) || !IsAgentSource(entry.Source))
                {
                    return route.Fail($"The source '{source}' cannot be set on an activity an agent works.");
                }

                route.Source = entry.Source;

                var userName = Normalize(request.AssignToUser);

                if (userName is not null)
                {
                    var user = await _userManager.FindByNameAsync(userName) ?? await _userManager.FindByIdAsync(userName);

                    if (user is null)
                    {
                        return route.Fail($"The user '{userName}' does not exist.");
                    }

                    route.OwnerId = await _userManager.GetUserIdAsync(user);
                    route.OwnerName = await _userManager.GetUserNameAsync(user);
                }

                break;

            case ActivityRerouteTarget.KeepRoute:
                if (route.CampaignId is null)
                {
                    return route.Fail("Keeping how the activity is worked changes only its campaign, and no campaign was named.");
                }

                break;
        }

        return route;
    }

    private async Task<IReadOnlyList<string>> FindOpenFollowUpIdsAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(activity.ContactContentItemId))
        {
            return [];
        }

        var contactId = activity.ContactContentItemId;
        var subject = activity.SubjectContentType;
        var since = activity.CreatedUtc;

        // The next attempt of the same work: an open activity for the same contact and subject, created after this one.
        var open = await _session.QueryIndex<OmnichannelActivityIndex>(
                index => index.ContactContentItemId == contactId &&
                    index.SubjectContentType == subject &&
                    index.CreatedUtc >= since &&
                    index.Status.IsIn(_openStatuses),
                collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken);

        return open
            .Where(index => !string.Equals(index.ItemId, activity.ItemId, StringComparison.Ordinal))
            .Select(index => index.ItemId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private async Task RerouteExistingAsync(IReadOnlyList<string> activityItemIds, ResolvedRoute route, ActivityRerouteResult result, CancellationToken cancellationToken)
    {
        // A record the dialer is dialing, or an agent is on a call for, belongs to that call until it ends.
        var inFlight = _dialerContributor is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : await _dialerContributor.GetActivitiesInFlightAsync(activityItemIds, cancellationToken);

        foreach (var activityItemId in activityItemIds)
        {
            if (inFlight.Contains(activityItemId))
            {
                Skip(result, activityItemId, "the dialer is dialing it or an agent is on its call");

                continue;
            }

            // Moving a waiting record commits the unit of work, and a committed session no longer tracks what it loaded
            // before, so each activity is read as the last commit left it.
            var activity = await _activityManager.FindByIdAsync(activityItemId, cancellationToken);

            if (activity is null || !_openStatuses.Contains(activity.Status))
            {
                Skip(result, activityItemId, "it has started or finished");

                continue;
            }

            if (await RerouteAsync(activity, route, result, cancellationToken))
            {
                result.ActivityItemIds.Add(activityItemId);
            }
        }
    }

    private async Task<bool> RerouteAsync(OmnichannelActivity activity, ResolvedRoute route, ActivityRerouteResult result, CancellationToken cancellationToken)
    {
        var activityItemId = activity.ItemId;
        var campaignId = route.CampaignId ?? Normalize(activity.CampaignId);
        var isDialerWork = ActivitySources.IsDialer(activity.Source) || !string.IsNullOrWhiteSpace(activity.DialerProfileId);

        switch (route.Target)
        {
            case ActivityRerouteTarget.Agent:
                if (isDialerWork && _dialerContributor is not null &&
                    await _dialerContributor.WithdrawAsync(activityItemId, cancellationToken) == ActivityDialerWithdrawResult.InFlight)
                {
                    Skip(result, activityItemId, "the dialer took it while it was being moved");

                    return false;
                }

                activity = await _activityManager.FindByIdAsync(activityItemId, cancellationToken);

                if (activity is null)
                {
                    return false;
                }

                ApplyAgentRoute(activity, route.Source, campaignId, route.OwnerId, route.OwnerName, _clock.UtcNow);
                await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);

                return true;

            case ActivityRerouteTarget.Dialer:
                if (!await CanQueueForDialerAsync(activity, route.Profile.ProfileId, campaignId, result, cancellationToken))
                {
                    return false;
                }

                var destination = await FindDialableDestinationAsync(activity);

                if (string.IsNullOrWhiteSpace(destination))
                {
                    Skip(result, activityItemId, "it has no number the dialer can call");

                    return false;
                }

                if (!await MoveCampaignAsync(activity, campaignId, result, cancellationToken))
                {
                    return false;
                }

                activity = await _activityManager.FindByIdAsync(activityItemId, cancellationToken);

                if (activity is null)
                {
                    return false;
                }

                ApplyDialerRoute(activity, route.Profile, campaignId, destination);
                await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);

                // Queued on its campaign, or kept in its place there under the new profile.
                await _dialerContributor.EnqueueAsync(activityItemId, campaignId, route.Profile, cancellationToken);

                return true;

            default:
                if (isDialerWork &&
                    !await CanQueueForDialerAsync(activity, Normalize(activity.DialerProfileId), campaignId, result, cancellationToken))
                {
                    return false;
                }

                if (!await MoveCampaignAsync(activity, campaignId, result, cancellationToken))
                {
                    return false;
                }

                activity = await _activityManager.FindByIdAsync(activityItemId, cancellationToken);

                if (activity is null)
                {
                    return false;
                }

                activity.CampaignId = campaignId;
                await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);

                return true;
        }
    }

    private async Task CreateNextAttemptAsync(OmnichannelActivity activity, ResolvedRoute route, int? delayMinutes, ActivityRerouteResult result, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var nextAttempt = OmnichannelActivityFollowUps.CreateNextAttempt(activity, now);
        nextAttempt.ScheduledUtc = now.AddMinutes(Math.Max(0, delayMinutes ?? 0));

        // The agent who finished the call keeps the follow-up, as a "Try again" action does unless it names someone.
        nextAttempt.AssignedToId = activity.CompletedById;
        nextAttempt.AssignedToUsername = activity.CompletedByUsername;

        if (!string.IsNullOrEmpty(nextAttempt.AssignedToId))
        {
            nextAttempt.AssignedToUtc = now;
            nextAttempt.AssignmentStatus = ActivityAssignmentStatus.Assigned;
        }

        var campaignId = route.CampaignId ?? Normalize(activity.CampaignId);

        switch (route.Target)
        {
            case ActivityRerouteTarget.Agent:
                ApplyAgentRoute(nextAttempt, route.Source, campaignId, route.OwnerId, route.OwnerName, now);
                break;

            case ActivityRerouteTarget.Dialer:
                if (!await CanQueueForDialerAsync(nextAttempt, route.Profile.ProfileId, campaignId, result, cancellationToken))
                {
                    return;
                }

                var destination = await FindDialableDestinationAsync(nextAttempt);

                if (string.IsNullOrWhiteSpace(destination))
                {
                    Skip(result, activity.ItemId, "its contact has no number the dialer can call");

                    return;
                }

                ApplyDialerRoute(nextAttempt, route.Profile, campaignId, destination);
                break;

            default:
                // Carried the same way as the finished activity, on its new campaign. A dialer record is queued again
                // under the profile it was dialed with.
                nextAttempt.CampaignId = campaignId;
                nextAttempt.DialerProfileId = activity.DialerProfileId;

                if (!string.IsNullOrWhiteSpace(nextAttempt.DialerProfileId) &&
                    !await CanQueueForDialerAsync(nextAttempt, nextAttempt.DialerProfileId, campaignId, result, cancellationToken))
                {
                    return;
                }

                ApplyInitialStatus(nextAttempt);
                break;
        }

        await _activityManager.CreateAsync(nextAttempt, cancellationToken: cancellationToken);

        var profile = route.Target == ActivityRerouteTarget.Dialer
            ? route.Profile
            : route.Target == ActivityRerouteTarget.KeepRoute && _dialerContributor is not null && !string.IsNullOrWhiteSpace(nextAttempt.DialerProfileId)
                ? await _dialerContributor.FindByIdAsync(nextAttempt.DialerProfileId, cancellationToken)
                : null;

        if (profile is not null)
        {
            await _dialerContributor.EnqueueAsync(nextAttempt.ItemId, campaignId, profile, cancellationToken);
        }

        result.ActivityItemIds.Add(nextAttempt.ItemId);
        result.CreatedNextAttempt = true;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Created attempt {AttemptNumber} ('{NextActivityId}') of finished activity '{ActivityId}', routed to {Target}, due {DueUtc:O}.",
                nextAttempt.Attempts,
                nextAttempt.ItemId.SanitizeLogValue(),
                activity.ItemId.SanitizeLogValue(),
                route.Target,
                nextAttempt.ScheduledUtc);
        }
    }

    private async Task<bool> CanQueueForDialerAsync(OmnichannelActivity activity, string profileId, string campaignId, ActivityRerouteResult result, CancellationToken cancellationToken)
    {
        if (_dialerContributor is null || profileId is null)
        {
            return true;
        }

        // Dialer work waits on its campaign's queue, which is what agents sign in to.
        if (campaignId is null)
        {
            Skip(result, activity.ItemId, "it has no campaign for the dialer to queue it on");

            return false;
        }

        var conflicts = await DialerCampaignProfileGuard.FindConflictsAsync(
            _dialerContributor,
            campaignId,
            profileId,
            new HashSet<string>(StringComparer.Ordinal) { activity.ItemId },
            cancellationToken);

        if (conflicts.Count > 0)
        {
            Skip(result, activity.ItemId, $"its campaign already has records waiting under {DialerCampaignProfileGuard.Describe(conflicts)}, and a campaign's waiting records must all use one dialer profile");

            return false;
        }

        return true;
    }

    private async Task<bool> MoveCampaignAsync(OmnichannelActivity activity, string campaignId, ActivityRerouteResult result, CancellationToken cancellationToken)
    {
        if (_dialerContributor is null ||
            campaignId is null ||
            string.Equals(Normalize(activity.CampaignId), campaignId, StringComparison.Ordinal))
        {
            return true;
        }

        if (await _dialerContributor.MoveToCampaignAsync(activity.ItemId, campaignId, cancellationToken) == ActivityDialerCampaignMoveResult.InFlight)
        {
            Skip(result, activity.ItemId, "the dialer took it while it was being moved");

            return false;
        }

        return true;
    }

    private Task<string> FindDialableDestinationAsync(OmnichannelActivity activity)
        => ActivityDialDestinations.FindAsync(activity, _contentManager, _contactMethods);

    private void Skip(ActivityRerouteResult result, string activityItemId, string reason)
    {
        result.Reason = $"Activity '{activityItemId}' was left as it was: {reason}.";

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Activity '{ActivityId}' was not rerouted: {Reason}.",
                activityItemId.SanitizeLogValue(),
                reason.SanitizeLogValue());
        }
    }

    private static void ApplyInitialStatus(OmnichannelActivity activity)
    {
        activity.Status = OmnichannelAutomationHelper.GetInitialActivityStatus(activity.InteractionType, !string.IsNullOrEmpty(activity.AssignedToId));

        // Re-arming an activity to a due status starts a fresh automated-processing budget.
        activity.ProcessingAttempts = 0;
    }

    private static void ClearReservation(OmnichannelActivity activity)
    {
        activity.ReservationId = null;
        activity.ReservedById = null;
        activity.ReservedByUsername = null;
        activity.ReservedUtc = null;
        activity.ReservationExpiresUtc = null;
    }

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class ResolvedRoute
    {
        public ActivityRerouteTarget Target { get; init; }

        public string CampaignId { get; init; }

        public ActivityDialerProfileDescriptor Profile { get; set; }

        public string Source { get; set; }

        public string OwnerId { get; set; }

        public string OwnerName { get; set; }

        public string Error { get; private set; }

        public ResolvedRoute Fail(string error)
        {
            Error = error;

            return this;
        }
    }
}
