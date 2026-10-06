using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// A search over the call recordings catalog. Only playable recordings (stored and not erased) are returned.
/// </summary>
public sealed class CallRecordingQuery
{
    /// <summary>
    /// Gets or sets the agent whose calls are listed. A caller who may only listen to their own calls always sets it
    /// to themselves.
    /// </summary>
    public string AgentUserId { get; set; }

    /// <summary>
    /// Gets or sets part of the customer's phone number to match.
    /// </summary>
    public string CustomerAddress { get; set; }

    /// <summary>
    /// Gets or sets the call direction to match.
    /// </summary>
    public InteractionDirection? Direction { get; set; }

    /// <summary>
    /// Gets or sets the kind of call to match.
    /// </summary>
    public CallRecordingSource? Source { get; set; }

    /// <summary>
    /// Gets or sets the earliest UTC start time to include.
    /// </summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the recordings must start before.
    /// </summary>
    public DateTime? ToUtc { get; set; }

    /// <summary>
    /// Gets or sets the 1-based page number.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of recordings per page.
    /// </summary>
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// One page of call recordings.
/// </summary>
public sealed class CallRecordingPage
{
    /// <summary>
    /// Gets an empty page.
    /// </summary>
    public static CallRecordingPage Empty { get; } = new()
    {
        Entries = [],
    };

    /// <summary>
    /// Gets or sets the total number of recordings that match.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the recordings on this page, newest first.
    /// </summary>
    public IReadOnlyList<CallRecording> Entries { get; set; } = [];
}
