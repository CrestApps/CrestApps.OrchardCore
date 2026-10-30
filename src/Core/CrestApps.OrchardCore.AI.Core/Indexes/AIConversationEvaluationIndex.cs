using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

/// <summary>
/// One row per <see cref="Conversations.AIConversationEvaluation"/>, which QA reports and alert checks read.
/// </summary>
public sealed class AIConversationEvaluationIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the identifier of the evaluation.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the AI session that was evaluated.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the AI profile that held the conversation.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the evaluation's label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the score out of 100.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Gets or sets whether the conversation passed.
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Gets or sets whether the customer's need was resolved.
    /// </summary>
    public bool Resolved { get; set; }

    /// <summary>
    /// Gets or sets the number of issues found.
    /// </summary>
    public int IssueCount { get; set; }

    /// <summary>
    /// Gets or sets the kinds of the issues found, comma separated, each once.
    /// </summary>
    public string IssueKinds { get; set; }

    /// <summary>
    /// Gets or sets when the conversation was evaluated.
    /// </summary>
    public DateTime EvaluatedUtc { get; set; }
}
