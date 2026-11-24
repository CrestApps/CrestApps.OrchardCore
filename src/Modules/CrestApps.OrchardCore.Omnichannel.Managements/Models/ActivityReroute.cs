namespace CrestApps.OrchardCore.Omnichannel.Managements.Models;

/// <summary>
/// Who works an activity after it is rerouted.
/// </summary>
public enum ActivityRerouteTarget
{
    /// <summary>
    /// An agent calls the contact: the activity leaves the dialer, takes a source the dialer does not drive, and goes
    /// to the named user or, when none is named, keeps the owner it has.
    /// </summary>
    Agent,

    /// <summary>
    /// The dialer calls the contact under a dialer profile, from the campaign's dialer queue.
    /// </summary>
    Dialer,

    /// <summary>
    /// The activity is worked the same way as before; only its campaign changes.
    /// </summary>
    KeepRoute,
}

/// <summary>
/// What an activity is rerouted to.
/// </summary>
public sealed class ActivityRerouteRequest
{
    /// <summary>
    /// Gets or sets who works the activity next.
    /// </summary>
    public ActivityRerouteTarget Target { get; set; }

    /// <summary>
    /// Gets or sets the campaign the activity moves to. Empty keeps its campaign.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the dialer profile that dials the activity. Required for <see cref="ActivityRerouteTarget.Dialer"/>.
    /// </summary>
    public string DialerProfileId { get; set; }

    /// <summary>
    /// Gets or sets the source of an activity an agent works, one that may be set by hand. Empty is <c>Manual</c>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the user name, or user id, of the agent who works the activity, for
    /// <see cref="ActivityRerouteTarget.Agent"/>. Empty keeps the owner the activity has.
    /// </summary>
    public string AssignToUser { get; set; }

    /// <summary>
    /// Gets or sets whether a finished activity with no open follow-up gets its next attempt created, already rerouted.
    /// </summary>
    public bool CreateNextAttempt { get; set; }

    /// <summary>
    /// Gets or sets how many minutes from now a created next attempt is due. Empty or zero is due now.
    /// </summary>
    public int? DelayMinutes { get; set; }
}

/// <summary>
/// How a reroute ended.
/// </summary>
public enum ActivityRerouteStatus
{
    /// <summary>
    /// At least one activity was rerouted or created.
    /// </summary>
    Rerouted,

    /// <summary>
    /// The activity is finished, it has no open follow-up, and none was to be created.
    /// </summary>
    NothingToReroute,

    /// <summary>
    /// The request could not be carried out, or every activity it named was left as it was.
    /// </summary>
    Failed,
}

/// <summary>
/// The outcome of rerouting an activity.
/// </summary>
public sealed class ActivityRerouteResult
{
    /// <summary>
    /// Gets or sets how the reroute ended.
    /// </summary>
    public ActivityRerouteStatus Status { get; set; }

    /// <summary>
    /// Gets the activities that were rerouted or created.
    /// </summary>
    public IList<string> ActivityItemIds { get; } = [];

    /// <summary>
    /// Gets or sets why the reroute failed, or why an activity was left as it was.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets whether the activity rerouted is a next attempt the reroute created.
    /// </summary>
    public bool CreatedNextAttempt { get; set; }

    internal static ActivityRerouteResult Failed(string reason)
        => new()
        {
            Status = ActivityRerouteStatus.Failed,
            Reason = reason,
        };
}
