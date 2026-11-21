namespace CrestApps.OrchardCore.Surveys.Core.Qti;

/// <summary>
/// A question read from an assessment item, in the terms of the question types.
/// </summary>
public sealed class QtiQuestion
{
    /// <summary>Gets or sets the question type, one of <see cref="QtiQuestionTypes"/>.</summary>
    public string QuestionType { get; set; }

    /// <summary>Gets or sets the prompt, as HTML.</summary>
    public string Prompt { get; set; }

    /// <summary>Gets or sets the points, from the outcome declarations and the response processing.</summary>
    public decimal Points { get; set; } = 1;

    /// <summary>Gets or sets the feedback shown whatever the answer, as HTML.</summary>
    public string GeneralFeedback { get; set; }

    /// <summary>Gets or sets the options of a choice question, or the items of an ordering question in their right order.</summary>
    public IList<QtiOption> Options { get; set; } = [];

    /// <summary>Gets or sets whether the options are shuffled.</summary>
    public bool Shuffle { get; set; }

    /// <summary>Gets or sets whether a partly right answer earns part of the points.</summary>
    public bool PartialCredit { get; set; }

    /// <summary>Gets or sets the answer of a true or false question.</summary>
    public bool TrueFalseAnswer { get; set; }

    /// <summary>Gets or sets the accepted answers of a short text question.</summary>
    public IList<string> AcceptedAnswers { get; set; } = [];

    /// <summary>Gets or sets whether letter case matters.</summary>
    public bool CaseSensitive { get; set; }

    /// <summary>Gets or sets the answer of a numeric question.</summary>
    public decimal NumericAnswer { get; set; }

    /// <summary>Gets or sets the tolerance of a numeric question.</summary>
    public decimal Tolerance { get; set; }

    /// <summary>Gets or sets the pairs of a matching question.</summary>
    public IList<(string Prompt, string Answer)> Pairs { get; set; } = [];

    /// <summary>Gets or sets the text of a fill in the blanks question, each blank written as <c>[[answer|other answer]]</c>.</summary>
    public string BlankText { get; set; }

    /// <summary>Gets or sets the choices no blank takes, of a drag into text or gapselect question.</summary>
    public IList<string> Distractors { get; set; } = [];

    /// <summary>Gets or sets whether the learner may choose several options of a survey question.</summary>
    public bool Multiple { get; set; }

    /// <summary>Gets or sets the guidance graders see, from a rubric block for scorers.</summary>
    public string GraderGuidance { get; set; }
}
