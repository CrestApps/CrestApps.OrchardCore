namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Explains, on the supervisor dashboard, why the work of each queue and campaign is or is not being handed to agents.
/// </summary>
public sealed class SupervisorDispatchStateViewModel
{
    /// <summary>
    /// Gets or sets the authoritative server UTC time the explanation was read at.
    /// </summary>
    public DateTime ServerTimeUtc { get; set; }

    /// <summary>
    /// Gets or sets the explanation of each queue and campaign the viewing supervisor oversees that has work pending, or is
    /// paused.
    /// </summary>
    public IList<SupervisorDispatchViewModel> Queues { get; set; } = [];
}

/// <summary>
/// Explains why one queue's, or one campaign's, pending work is or is not being handed to agents.
/// </summary>
public sealed class SupervisorDispatchViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the queue; for a campaign, of its own queue.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the campaign, or <see langword="null"/> for a queue that is not a campaign's.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the queue or campaign.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the number of records pending.
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// Gets or sets the number of pending records the breakdown accounts for; lower than <see cref="PendingCount"/> when
    /// the campaign has more than one pass reads.
    /// </summary>
    public int EvaluatedCount { get; set; }

    /// <summary>
    /// Gets or sets how many of the evaluated records can be handed to an agent right now.
    /// </summary>
    public int DialableNowCount { get; set; }

    /// <summary>
    /// Gets or sets how many of the evaluated records are held back, for any reason.
    /// </summary>
    public int HeldBackCount { get; set; }

    /// <summary>
    /// Gets or sets the number of agents signed in to the queue or campaign.
    /// </summary>
    public int SignedInAgentCount { get; set; }

    /// <summary>
    /// Gets or sets the number of signed-in agents who are Available.
    /// </summary>
    public int AvailableAgentCount { get; set; }

    /// <summary>
    /// Gets or sets the name of the reason the whole queue is paused, or <see langword="null"/> when it is running.
    /// </summary>
    public string QueueBlocker { get; set; }

    /// <summary>
    /// Gets or sets the evaluated records grouped by what holds them back.
    /// </summary>
    public IList<SupervisorDispatchBucketViewModel> Buckets { get; set; } = [];

    /// <summary>
    /// Gets or sets what routing found the last time it looked for an agent and found none, or <see langword="null"/> when
    /// it has not failed lately.
    /// </summary>
    public SupervisorDispatchRoutingViewModel Routing { get; set; }
}

/// <summary>
/// The records of a queue that share one reason for not being handed to an agent.
/// </summary>
public sealed class SupervisorDispatchBucketViewModel
{
    /// <summary>
    /// Gets or sets the name of the reason, or <c>DialableNow</c> when nothing holds the records back.
    /// </summary>
    public string Blocker { get; set; }

    /// <summary>
    /// Gets or sets the number of records.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets when the first of the records becomes due, for a wait that ends by itself.
    /// </summary>
    public DateTime? NextDueUtc { get; set; }
}

/// <summary>
/// What routing found when it last looked for an agent to take a queue's next item and found none.
/// </summary>
public sealed class SupervisorDispatchRoutingViewModel
{
    /// <summary>
    /// Gets or sets when routing decided.
    /// </summary>
    public DateTime OccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets the reason routing gave for assigning nobody.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the number of agents routing considered.
    /// </summary>
    public int CandidateCount { get; set; }

    /// <summary>
    /// Gets or sets the reasons routing turned agents away, most common first.
    /// </summary>
    public IList<SupervisorDispatchCandidateReasonViewModel> CandidateReasons { get; set; } = [];
}

/// <summary>
/// A reason routing turned agents away, and how many.
/// </summary>
public sealed class SupervisorDispatchCandidateReasonViewModel
{
    /// <summary>
    /// Gets or sets the reason routing gave.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the number of agents turned away for it.
    /// </summary>
    public int AgentCount { get; set; }
}
