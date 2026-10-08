using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

/// <summary>
/// Lets the person completing or reviewing an activity listen to the recordings of its calls on the activity's page.
/// </summary>
/// <remarks>
/// Registered only with the Call Recording feature. The recordings listed, and who may hear them, follow the call
/// recordings page exactly: a user who may hear only their own calls is shown only those, and a user who may hear none
/// is shown nothing at all. Playback goes through the recordings page's media endpoint, which checks the same rule
/// again and audits the listen.
/// </remarks>
internal sealed class OmnichannelActivityCallRecordingsDisplayDriver : DisplayDriver<OmnichannelActivity>
{
    private readonly ActivityCallRecordingLookup _lookup;
    private readonly CallRecordingAccessEvaluator _accessEvaluator;
    private readonly CallRecordingAgentNameResolver _nameResolver;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILocalClock _localClock;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityCallRecordingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="lookup">Finds the recordings of the activity's calls.</param>
    /// <param name="accessEvaluator">Works out which recordings the viewer may hear.</param>
    /// <param name="nameResolver">Names the agents on the calls.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, read for the viewer.</param>
    /// <param name="localClock">The viewer's local clock.</param>
    public OmnichannelActivityCallRecordingsDisplayDriver(
        ActivityCallRecordingLookup lookup,
        CallRecordingAccessEvaluator accessEvaluator,
        CallRecordingAgentNameResolver nameResolver,
        IHttpContextAccessor httpContextAccessor,
        ILocalClock localClock)
    {
        _lookup = lookup;
        _accessEvaluator = accessEvaluator;
        _nameResolver = nameResolver;
        _httpContextAccessor = httpContextAccessor;
        _localClock = localClock;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> EditAsync(OmnichannelActivity activity, BuildEditorContext context)
    {
        // Shown while the activity is being completed, and on a completed activity's page, which is rendered outside
        // the completion group. Anywhere else the recordings are not looked up at all.
        var group = activity.Status == ActivityStatus.Completed
            ? string.Empty
            : OmnichannelConstants.CompleteActivityGroup;

        if (!string.Equals(context.GroupId ?? string.Empty, group, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(activity.ItemId))
        {
            return null;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var access = await _accessEvaluator.GetAccessAsync(httpContext?.User);

        if (!access.CanListOwn)
        {
            return null;
        }

        var cancellationToken = httpContext?.RequestAborted ?? CancellationToken.None;
        var recordings = (await _lookup.ListPlayableAsync(activity.ItemId, cancellationToken))
            .Where(access.CanHear)
            .ToArray();

        if (recordings.Length == 0)
        {
            return null;
        }

        var names = await _nameResolver.GetNamesAsync(recordings.Select(recording => recording.AgentUserId), cancellationToken);
        var items = new List<ActivityCallRecordingItemViewModel>(recordings.Length);

        foreach (var recording in recordings)
        {
            items.Add(new ActivityCallRecordingItemViewModel
            {
                Recording = recording,
                // The stored time is UTC whatever kind it was read back as; an unspecified kind would be taken as
                // server-local time by the conversion.
                StartedLocal = (await _localClock.ConvertToLocalAsync(
                    new DateTimeOffset(DateTime.SpecifyKind(recording.StartedUtc, DateTimeKind.Utc)))).DateTime,
                AgentName = CallRecordingAgentNameResolver.NameOf(recording.AgentUserId, names),
            });
        }

        var result = Initialize<ActivityCallRecordingsViewModel>("OmnichannelActivityCallRecordings_Edit", model =>
        {
            model.Items = items;
        }).Location("Content:4.5")
        .OnGroup(OmnichannelConstants.CompleteActivityGroup);

        // A completed activity is shown outside the completion group, and its calls can still be listened to.
        if (activity.Status == ActivityStatus.Completed)
        {
            result.OnGroup(string.Empty);
        }

        return result;
    }
}
