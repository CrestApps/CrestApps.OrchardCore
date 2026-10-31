using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// What an automated AI call or text conversation is started with.
/// </summary>
public sealed class AIActivityStartRequest
{
    /// <summary>
    /// Gets or sets the channel: <see cref="OmnichannelConstants.Channels.Phone"/> or <see cref="OmnichannelConstants.Channels.Sms"/>.
    /// </summary>
    public string Channel { get; set; } = OmnichannelConstants.Channels.Phone;

    /// <summary>
    /// Gets or sets the AI profile that holds the conversation.
    /// </summary>
    public string AIProfileId { get; set; }

    /// <summary>
    /// Gets or sets the phone number called or texted.
    /// </summary>
    public string Destination { get; set; }

    /// <summary>
    /// Gets or sets the number the call or text comes from, when not the default one.
    /// </summary>
    public string ChannelEndpointId { get; set; }

    /// <summary>
    /// Gets or sets the campaign it belongs to.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the content type of the activity's subject.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the contact it is with.
    /// </summary>
    public string ContactContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the conversation variables the profile sees.
    /// </summary>
    public IList<KeyValuePair<string, string>> Variables { get; set; } = [];

    /// <summary>
    /// Gets or sets where the activity came from, from <see cref="ActivitySources"/>.
    /// </summary>
    public string Source { get; set; } = ActivitySources.Workflow;

    /// <summary>
    /// Gets or sets when to start, or <see langword="null"/> for now.
    /// </summary>
    public DateTime? ScheduledUtc { get; set; }
}

/// <summary>
/// Creates the automated activity that makes an AI call or starts an AI text conversation. The dialer or the SMS
/// sender picks it up like any automated activity.
/// </summary>
public sealed class AIActivityStarter
{
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IContentManager _contentManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIActivityStarter"/> class.
    /// </summary>
    /// <param name="activityManager">The activity manager.</param>
    /// <param name="contentManager">Finds the contact and makes the subject.</param>
    /// <param name="clock">The clock.</param>
    public AIActivityStarter(IOmnichannelActivityManager activityManager, IContentManager contentManager, IClock clock)
    {
        _activityManager = activityManager;
        _contentManager = contentManager;
        _clock = clock;
    }

    /// <summary>
    /// Creates the activity.
    /// </summary>
    /// <param name="request">What it is started with.</param>
    /// <returns>The activity.</returns>
    public async Task<OmnichannelActivity> StartAsync(AIActivityStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AIProfileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Destination);

        var now = _clock.UtcNow;
        var channel = string.Equals(request.Channel, OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase)
            ? OmnichannelConstants.Channels.Sms
            : OmnichannelConstants.Channels.Phone;

        var activity = await _activityManager.NewAsync();
        activity.Kind = channel == OmnichannelConstants.Channels.Sms ? ActivityKind.Sms : ActivityKind.Call;
        activity.Source = string.IsNullOrWhiteSpace(request.Source) ? ActivitySources.Workflow : request.Source;
        activity.Channel = channel;
        activity.InteractionType = ActivityInteractionType.Automated;
        activity.AIProfileId = request.AIProfileId;
        activity.ChannelEndpointId = string.IsNullOrWhiteSpace(request.ChannelEndpointId) ? null : request.ChannelEndpointId;
        activity.PreferredDestination = request.Destination.Trim();
        activity.CampaignId = string.IsNullOrWhiteSpace(request.CampaignId) ? null : request.CampaignId;
        activity.SubjectContentType = string.IsNullOrWhiteSpace(request.SubjectContentType) ? null : request.SubjectContentType;
        activity.AssignmentStatus = ActivityAssignmentStatus.Available;
        activity.Status = OmnichannelAutomationHelper.GetInitialActivityStatus(ActivityInteractionType.Automated, hasAssignedUser: false);
        activity.ScheduledUtc = request.ScheduledUtc is { } scheduled && scheduled > now ? scheduled : now;
        activity.CreatedUtc = now;
        activity.ContactResolutionStatus = ContactResolutionStatus.Unresolved;

        if (!string.IsNullOrWhiteSpace(request.ContactContentItemId))
        {
            var contact = await _contentManager.GetAsync(request.ContactContentItemId, VersionOptions.Latest);

            if (contact is not null)
            {
                activity.ContactContentItemId = contact.ContentItemId;
                activity.ContactContentType = contact.ContentType;
                activity.ContactResolutionStatus = ContactResolutionStatus.Resolved;
                activity.ContactResolvedUtc = now;
            }
        }

        if (!string.IsNullOrEmpty(activity.SubjectContentType))
        {
            activity.Subject = await _contentManager.NewAsync(activity.SubjectContentType);
        }

        if (request.Variables?.Count > 0)
        {
            activity.SetConversationVariables(request.Variables);
        }

        await _activityManager.CreateAsync(activity);

        return activity;
    }
}
