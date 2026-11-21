namespace CrestApps.OrchardCore.Surveys.Core.Qti;

/// <summary>
/// The kinds of question the QTI reader recognizes, before <see cref="QtiQuestionMapper"/> turns them into Surveys
/// question types.
/// </summary>
public static class QtiQuestionTypes
{
    /// <summary>
    /// One right choice.
    /// </summary>
    public const string SingleChoice = "SingleChoice";

    /// <summary>
    /// Several right choices.
    /// </summary>
    public const string MultipleChoice = "MultipleChoice";

    /// <summary>
    /// True or false.
    /// </summary>
    public const string TrueFalse = "TrueFalse";

    /// <summary>
    /// A short typed answer.
    /// </summary>
    public const string ShortText = "ShortText";

    /// <summary>
    /// A number within a tolerance.
    /// </summary>
    public const string Numeric = "Numeric";

    /// <summary>
    /// Prompts matched to answers.
    /// </summary>
    public const string Matching = "Matching";

    /// <summary>
    /// Items put in order.
    /// </summary>
    public const string Ordering = "Ordering";

    /// <summary>
    /// Typed words in blanks.
    /// </summary>
    public const string FillInBlanks = "FillInBlanks";

    /// <summary>
    /// A written answer a grader scores.
    /// </summary>
    public const string Essay = "Essay";

    /// <summary>
    /// Words dragged into blanks.
    /// </summary>
    public const string DragIntoText = "DragIntoText";

    /// <summary>
    /// A word picked for each blank.
    /// </summary>
    public const string GapSelect = "GapSelect";

    /// <summary>
    /// Text with no answer.
    /// </summary>
    public const string Description = "Description";
}
