namespace CrestApps.OrchardCore.AI.Chat.Interactions.Models;

/// <summary>
/// One piece of feedback the author gave on an assistant reply during role-play.
/// </summary>
public sealed class ProfileBuilderFeedback
{
    /// <summary>
    /// Gets or sets the unique identifier of the feedback.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets what the author asked for.
    /// </summary>
    public ProfileBuilderFeedbackKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the assistant prompt the feedback is about. The prompt may no longer exist
    /// once the reply was regenerated; the text below is what the coach and the replay work from.
    /// </summary>
    public string PromptId { get; set; }

    /// <summary>
    /// Gets or sets the customer message the assistant was answering.
    /// </summary>
    public string CustomerMessage { get; set; }

    /// <summary>
    /// Gets or sets the assistant reply the feedback is about.
    /// </summary>
    public string AssistantReply { get; set; }

    /// <summary>
    /// Gets or sets the conversation before the customer message, so the same situation can be replayed later.
    /// </summary>
    public List<ProfileBuilderTurn> Context { get; set; } = [];

    /// <summary>
    /// Gets or sets the author's comment. Empty for an approval without a comment.
    /// </summary>
    public string Comment { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the reply was removed from the conversation and generated again.
    /// </summary>
    public bool WasRegenerated { get; set; }

    /// <summary>
    /// Gets or sets the number of the revision this feedback produced, or <see langword="null"/> when it produced none.
    /// </summary>
    public int? RevisionNumber { get; set; }

    /// <summary>
    /// Gets or sets when the feedback was given.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// What the author asked for when giving feedback on a reply.
/// </summary>
public enum ProfileBuilderFeedbackKind
{
    /// <summary>
    /// Improve the instructions from the comment, and keep the reply.
    /// </summary>
    Feedback,

    /// <summary>
    /// Improve the instructions from the comment, then generate the reply again with them.
    /// </summary>
    FeedbackAndRegenerate,

    /// <summary>
    /// The reply is what the author wants. It becomes an example a later rewrite must not break.
    /// </summary>
    Approved,
}

/// <summary>
/// One turn of a conversation, as the coach and the replay see it.
/// </summary>
public sealed class ProfileBuilderTurn
{
    /// <summary>
    /// Gets or sets who spoke: <c>user</c> for the customer, <c>assistant</c> for the AI.
    /// </summary>
    public string Role { get; set; }

    /// <summary>
    /// Gets or sets what was said.
    /// </summary>
    public string Text { get; set; }
}
