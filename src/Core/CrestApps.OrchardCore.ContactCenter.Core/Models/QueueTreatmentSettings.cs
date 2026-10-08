namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What a caller hears while they wait. Every value is off by default: a queue that has configured nothing has
/// not asked for its callers to be talked at, and silence is a better default than an announcement nobody chose.
/// </summary>
public sealed class QueueTreatmentSettings
{
    /// <summary>
    /// Gets or sets the message played once when the caller enters the queue.
    /// </summary>
    public string WelcomeMessage { get; set; }

    /// <summary>
    /// Gets or sets how often the periodic announcement repeats, in seconds. Zero means never.
    /// </summary>
    public int AnnouncementIntervalSeconds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the announcement states the caller's position in line.
    /// </summary>
    public bool AnnouncePosition { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the announcement states the estimated wait.
    /// </summary>
    public bool AnnounceEstimatedWait { get; set; }

    /// <summary>
    /// Gets or sets the media reference for hold music.
    /// </summary>
    public string HoldMusicMediaId { get; set; }

    /// <summary>
    /// Gets or sets the DTMF key that accepts a callback, or null when no callback is offered.
    /// </summary>
    public string CallbackDtmfKey { get; set; }

    /// <summary>
    /// Gets or sets how long the caller waits before the callback is offered. Offering immediately reads as the
    /// queue trying to get rid of them; offering too late means they have already gone.
    /// </summary>
    public int CallbackOfferAfterSeconds { get; set; }

    /// <summary>
    /// Gets or sets the shortest wait the queue will quote. Telling the next caller in line "no wait" is a
    /// promise the queue cannot keep, because the agent still has to finish the call they are on.
    /// </summary>
    public int MinimumEstimateSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the longest wait the queue will quote. Past this the announcement says nothing about time,
    /// because a four-hour estimate is not information, it is an invitation to hang up angry.
    /// </summary>
    public int MaximumEstimateSeconds { get; set; } = 1_800;

    /// <summary>
    /// Gets or sets the handle time the estimate extrapolates from, in seconds. Zero means no estimate is
    /// spoken at all.
    /// </summary>
    /// <remarks>
    /// Configured rather than measured. The metric store counts events, not handle time, and deriving an average
    /// live would put a scan of ended interactions on a sweep that runs every few seconds for every queue. A
    /// quoted wait that is wrong is worse than no quoted wait, so this stays an explicit expectation an operator
    /// sets and can correct.
    /// </remarks>
    public int AverageHandleTimeSeconds { get; set; }
}
