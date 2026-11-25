namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Who makes the call a subject action's follow-up activity stands for.
/// </summary>
public enum FollowUpRoutingTarget
{
    /// <summary>
    /// The follow-up is worked the way the activity it follows was: a dialer record goes back to the dialer.
    /// </summary>
    SameAsBefore,

    /// <summary>
    /// An agent makes the call by hand. The follow-up is not dialer work, so the dialer never calls it.
    /// </summary>
    Agent,

    /// <summary>
    /// The dialer calls the contact under a dialer profile, from the campaign's dialer queue.
    /// </summary>
    Dialer,
}

/// <summary>
/// How the follow-up activity a "Try again" or "New activity" subject action creates is routed: by an agent or by the
/// dialer, on which campaign, under which dialer profile, with which source. Stored with Put/TryGet on
/// <see cref="SubjectAction"/>.
/// </summary>
public sealed class FollowUpRoutingMetadata
{
    /// <summary>
    /// Gets or sets who makes the follow-up call.
    /// </summary>
    public FollowUpRoutingTarget Target { get; set; }

    /// <summary>
    /// Gets or sets the campaign the follow-up belongs to. Empty keeps the campaign it would have had.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the dialer profile that dials the follow-up, for <see cref="FollowUpRoutingTarget.Dialer"/>.
    /// </summary>
    public string DialerProfileId { get; set; }

    /// <summary>
    /// Gets or sets the source of a follow-up an agent works, for <see cref="FollowUpRoutingTarget.Agent"/>. Empty is
    /// <see cref="ActivitySources.Manual"/>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Determines whether the action changes anything about how its follow-up is routed.
    /// </summary>
    /// <returns><see langword="true"/> when the follow-up goes to someone else or another campaign.</returns>
    public bool ChangesRouting()
        => Target != FollowUpRoutingTarget.SameAsBefore || !string.IsNullOrWhiteSpace(CampaignId);
}
