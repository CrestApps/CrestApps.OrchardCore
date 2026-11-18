namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// One question as it was drawn into an attempt: everything shown and everything that scores it is copied here, so the
/// attempt reads the same after the question changes or its version is pruned.
/// </summary>
public sealed class AttemptItem
{
    /// <summary>
    /// Gets or sets the position in the attempt, from zero.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the question, for reference.
    /// </summary>
    public string QuestionId { get; set; }

    /// <summary>
    /// Gets or sets the version of the question that was drawn, for reference.
    /// </summary>
    public string QuestionVersionId { get; set; }

    /// <summary>
    /// Gets or sets the question type.
    /// </summary>
    public string QuestionType { get; set; }

    /// <summary>
    /// Gets or sets the title of the question.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the prompt, as HTML.
    /// </summary>
    public string Prompt { get; set; }

    /// <summary>
    /// Gets or sets the options, prompts or items shown, in the order shown.
    /// </summary>
    public IList<AttemptChoice> Choices { get; set; } = [];

    /// <summary>
    /// Gets or sets the answers a matching question offers, in the order shown.
    /// </summary>
    public IList<AttemptChoice> Answers { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of blanks of a fill in the blanks question.
    /// </summary>
    public int BlankCount { get; set; }

    /// <summary>
    /// Gets or sets the text segments around the blanks of a fill in the blanks question.
    /// </summary>
    public IList<string> Segments { get; set; } = [];

    /// <summary>
    /// Gets or sets the word limits of an essay.
    /// </summary>
    public int MinWords { get; set; }

    /// <summary>
    /// Gets or sets the word limit of an essay.
    /// </summary>
    public int MaxWords { get; set; }

    /// <summary>
    /// Gets or sets the answer key. It is never sent to the learner before the review policy allows.
    /// </summary>
    public AttemptKey Key { get; set; } = new();

    /// <summary>
    /// Gets or sets the points the item is worth.
    /// </summary>
    public decimal Points { get; set; }

    /// <summary>
    /// Gets or sets the general feedback.
    /// </summary>
    public string Feedback { get; set; }

    /// <summary>
    /// Gets or sets the learner response.
    /// </summary>
    public AttemptResponse Response { get; set; }

    /// <summary>
    /// Gets or sets the score given automatically, or <see langword="null"/> when a grader must mark it.
    /// </summary>
    public decimal? AutoScore { get; set; }

    /// <summary>
    /// Gets or sets the score a grader gave, or <see langword="null"/>.
    /// </summary>
    public decimal? ManualScore { get; set; }

    /// <summary>
    /// Gets or sets the comment of the grader.
    /// </summary>
    public string GraderComment { get; set; }

    /// <summary>
    /// Gets or sets whether the grader saved the manual score after using scoring help (a suggestion from AI). AI never
    /// saves a score itself; this records that the grader's score was AI-assisted.
    /// </summary>
    public bool AIAssisted { get; set; }

    /// <summary>
    /// Gets or sets the grader to whom scoring help gave a suggestion for this item since its last manual score. The next
    /// score that grader saves is AI-assisted; recorded on the server, never sent by the browser.
    /// </summary>
    public string ScoringHelpGivenTo { get; set; }

    /// <summary>
    /// Gets the score that counts: the grader score when there is one, else the automatic score.
    /// </summary>
    public decimal? Score => ManualScore ?? AutoScore;
}

/// <summary>
/// An option, prompt, answer or item as shown in an attempt.
/// </summary>
public sealed class AttemptChoice
{
    /// <summary>
    /// Gets or sets the stable id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the feedback shown in the review when it was chosen.
    /// </summary>
    public string Feedback { get; set; }
}

/// <summary>
/// The answer key of an attempt item.
/// </summary>
public sealed class AttemptKey
{
    /// <summary>
    /// Gets or sets the ids of the correct options, or of the items in their right order.
    /// </summary>
    public IList<string> ChoiceIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the accepted texts, or for each blank its accepted texts joined with a bar.
    /// </summary>
    public IList<string> Texts { get; set; } = [];

    /// <summary>
    /// Gets or sets the number answer.
    /// </summary>
    public decimal? Number { get; set; }

    /// <summary>
    /// Gets or sets the tolerance of the number answer.
    /// </summary>
    public decimal Tolerance { get; set; }

    /// <summary>
    /// Gets or sets, for a matching question, the answer id of each prompt id.
    /// </summary>
    public IDictionary<string, string> Pairs { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets whether letter case matters.
    /// </summary>
    public bool CaseSensitive { get; set; }

    /// <summary>
    /// Gets or sets whether partial answers earn part of the points.
    /// </summary>
    public bool PartialCredit { get; set; }
}

/// <summary>
/// What a learner answered.
/// </summary>
public sealed class AttemptResponse
{
    /// <summary>
    /// Gets or sets the chosen option ids, or the item ids in the order given.
    /// </summary>
    public IList<string> ChoiceIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the text answers: one for short text and essay, one per blank.
    /// </summary>
    public IList<string> Texts { get; set; } = [];

    /// <summary>
    /// Gets or sets the number answer.
    /// </summary>
    public decimal? Number { get; set; }

    /// <summary>
    /// Gets or sets, for a matching question, the answer id chosen for each prompt id.
    /// </summary>
    public IDictionary<string, string> Pairs { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets when the answer was saved, in UTC.
    /// </summary>
    public DateTime AnsweredUtc { get; set; }
}
