namespace CrestApps.OrchardCore.Learning.Assessments.ViewModels;

/// <summary>
/// A row of options, pairs or items in a question editor.
/// </summary>
public class OptionRowViewModel
{
    /// <summary>Gets or sets the stable id; empty for a new row.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the text, or the prompt of a pair.</summary>
    public string Text { get; set; }

    /// <summary>Gets or sets the answer of a matching pair.</summary>
    public string Answer { get; set; }

    /// <summary>Gets or sets whether the option is correct.</summary>
    public bool Correct { get; set; }

    /// <summary>Gets or sets the feedback.</summary>
    public string Feedback { get; set; }
}

/// <summary>
/// The editor of a question with rows: choice, matching and ordering questions.
/// </summary>
public class RowsQuestionViewModel
{
    /// <summary>Gets or sets the rows.</summary>
    public IList<OptionRowViewModel> Rows { get; set; } = [];

    /// <summary>Gets or sets whether the options are shuffled.</summary>
    public bool Shuffle { get; set; }

    /// <summary>Gets or sets whether partial answers earn part of the points.</summary>
    public bool PartialCredit { get; set; }

    /// <summary>Gets or sets whether several options may be right.</summary>
    public bool Multiple { get; set; }

    /// <summary>Gets or sets which editor this is: Choice, Matching or Ordering.</summary>
    public string Kind { get; set; }
}

/// <summary>
/// The editor of the simple question parts: true or false, short text, numeric, fill in the blanks and essay.
/// </summary>
public class SimpleQuestionViewModel
{
    /// <summary>Gets or sets the answer of a true or false question.</summary>
    public bool Answer { get; set; }

    /// <summary>Gets or sets the accepted answers, one a line.</summary>
    public string AcceptedAnswers { get; set; }

    /// <summary>Gets or sets whether letter case matters.</summary>
    public bool CaseSensitive { get; set; }

    /// <summary>Gets or sets the number answer.</summary>
    public decimal NumberAnswer { get; set; }

    /// <summary>Gets or sets the tolerance.</summary>
    public decimal Tolerance { get; set; }

    /// <summary>Gets or sets the text with blanks.</summary>
    public string Text { get; set; }

    /// <summary>Gets or sets the fewest words.</summary>
    public int MinWords { get; set; }

    /// <summary>Gets or sets the most words.</summary>
    public int MaxWords { get; set; }

    /// <summary>Gets or sets the guidance for graders.</summary>
    public string GraderGuidance { get; set; }
}
