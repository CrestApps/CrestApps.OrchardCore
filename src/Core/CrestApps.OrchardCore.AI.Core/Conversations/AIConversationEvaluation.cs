namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// How a conversation scored against quality criteria: a score out of 100, whether it passed, each criterion with
/// whether it was met, and the problems found, each with the line of the transcript that shows it.
/// </summary>
public sealed class AIConversationEvaluation
{
    /// <summary>
    /// Gets or sets the unique identifier of this evaluation.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the AI session that was evaluated, or <see langword="null"/> for a simulated conversation.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the AI profile that held the conversation.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the version of the profile that held the conversation, when it is known.
    /// </summary>
    public int? ProfileVersion { get; set; }

    /// <summary>
    /// Gets or sets a free label that groups evaluations, such as a QA cohort, a test suite or the variant of a
    /// split.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the score out of 100.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Gets or sets the lowest score that passes.
    /// </summary>
    public int PassingScore { get; set; }

    /// <summary>
    /// Gets or sets whether the conversation passed: the score reached <see cref="PassingScore"/> and every
    /// criterion was met.
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Gets or sets whether the customer's need was resolved.
    /// </summary>
    public bool Resolved { get; set; }

    /// <summary>
    /// Gets or sets the customer's sentiment at the end: <c>Positive</c>, <c>Neutral</c> or <c>Negative</c>.
    /// </summary>
    public string Sentiment { get; set; }

    /// <summary>
    /// Gets or sets a short explanation of the score.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets each criterion and whether it was met.
    /// </summary>
    public List<AIConversationCriterionResult> Criteria { get; set; } = [];

    /// <summary>
    /// Gets or sets the problems found.
    /// </summary>
    public List<AIConversationIssue> Issues { get; set; } = [];

    /// <summary>
    /// Gets or sets when the conversation was evaluated.
    /// </summary>
    public DateTime EvaluatedUtc { get; set; }
}

/// <summary>
/// Whether one criterion was met.
/// </summary>
public sealed class AIConversationCriterionResult
{
    /// <summary>
    /// Gets or sets the criterion as it was written.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets whether the conversation met it.
    /// </summary>
    public bool Met { get; set; }

    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public string Reason { get; set; }
}

/// <summary>
/// A problem found in a conversation.
/// </summary>
public sealed class AIConversationIssue
{
    /// <summary>
    /// Gets or sets the kind of problem. See <see cref="AIConversationIssueKinds"/>.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets what went wrong.
    /// </summary>
    public string Detail { get; set; }

    /// <summary>
    /// Gets or sets the line of the transcript that shows it.
    /// </summary>
    public string Quote { get; set; }
}

/// <summary>
/// The kinds of problem an evaluation reports.
/// </summary>
public static class AIConversationIssueKinds
{
    /// <summary>
    /// The customer's need was left unresolved.
    /// </summary>
    public const string Unresolved = "Unresolved";

    /// <summary>
    /// The AI stated something it had no source for.
    /// </summary>
    public const string Hallucination = "Hallucination";

    /// <summary>
    /// A tool the AI called failed, or the AI acted as if it had succeeded when it had not.
    /// </summary>
    public const string ToolFailure = "ToolFailure";

    /// <summary>
    /// The AI broke its instructions or a policy.
    /// </summary>
    public const string PolicyViolation = "PolicyViolation";

    /// <summary>
    /// The AI was rude, cold or confusing.
    /// </summary>
    public const string Tone = "Tone";

    /// <summary>
    /// The AI repeated itself or went in circles.
    /// </summary>
    public const string Repetition = "Repetition";

    /// <summary>
    /// A transfer or handoff went wrong or should have happened sooner.
    /// </summary>
    public const string Transfer = "Transfer";

    /// <summary>
    /// Anything else.
    /// </summary>
    public const string Other = "Other";

    /// <summary>
    /// Every kind, in the order the evaluator is told about them.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        Unresolved,
        Hallucination,
        ToolFailure,
        PolicyViolation,
        Tone,
        Repetition,
        Transfer,
        Other,
    ];
}
