namespace CrestApps.OrchardCore.AI.Chat.Interactions.Models;

/// <summary>
/// The result of replaying every reply the author gave feedback on against the current instructions.
/// </summary>
public sealed class ProfileBuilderReplay
{
    /// <summary>
    /// Gets or sets the revision the replay ran against.
    /// </summary>
    public int RevisionNumber { get; set; }

    /// <summary>
    /// Gets or sets when the replay ran.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets one result per replayed case, in the order the feedback was given.
    /// </summary>
    public List<ProfileBuilderReplayCase> Cases { get; set; } = [];
}

/// <summary>
/// One replayed case: a customer message the author gave feedback on, answered again with the current instructions.
/// </summary>
public sealed class ProfileBuilderReplayCase
{
    /// <summary>
    /// Gets or sets the identifier of the feedback the case comes from.
    /// </summary>
    public string FeedbackId { get; set; }

    /// <summary>
    /// Gets or sets the customer message that was replayed.
    /// </summary>
    public string CustomerMessage { get; set; }

    /// <summary>
    /// Gets or sets the reply the current instructions produced.
    /// </summary>
    public string Reply { get; set; }

    /// <summary>
    /// Gets or sets whether the reply honours the feedback.
    /// </summary>
    public ProfileBuilderReplayVerdict Verdict { get; set; }

    /// <summary>
    /// Gets or sets why the judge reached its verdict.
    /// </summary>
    public string Reason { get; set; }
}

/// <summary>
/// Whether a replayed reply honours the feedback it was replayed for.
/// </summary>
public enum ProfileBuilderReplayVerdict
{
    /// <summary>
    /// The judge could not tell, or the case could not be replayed.
    /// </summary>
    Unknown,

    /// <summary>
    /// The reply honours the feedback.
    /// </summary>
    Pass,

    /// <summary>
    /// The reply repeats the mistake the feedback was about, or breaks an approved example.
    /// </summary>
    Fail,
}
