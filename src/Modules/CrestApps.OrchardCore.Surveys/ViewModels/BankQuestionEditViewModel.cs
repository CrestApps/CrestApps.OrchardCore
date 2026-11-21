using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The page of a bank question: the shared question editor and the bank's own fields.
/// </summary>
public class BankQuestionEditViewModel
{
    /// <summary>
    /// Gets or sets the id of the bank.
    /// </summary>
    public string BankId { get; set; }

    /// <summary>
    /// Gets or sets the title of the bank.
    /// </summary>
    public string BankTitle { get; set; }

    /// <summary>
    /// Gets or sets the id of the question, or <see langword="null"/> for a new one.
    /// </summary>
    public string QuestionId { get; set; }

    /// <summary>
    /// Gets or sets the question type.
    /// </summary>
    public string ControlType { get; set; }

    /// <summary>
    /// Gets or sets the name of the question type.
    /// </summary>
    public string ControlTypeName { get; set; }

    /// <summary>
    /// Gets or sets the question editor.
    /// </summary>
    public IShape Editor { get; set; }

    /// <summary>
    /// Gets or sets the tags, separated by commas.
    /// </summary>
    public string Tags { get; set; }

    /// <summary>
    /// Gets or sets the difficulty from 1 to 5.
    /// </summary>
    public int Difficulty { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether pools no longer draw the question.
    /// </summary>
    public bool Retired { get; set; }

    /// <summary>
    /// Gets or sets the revision.
    /// </summary>
    public int Revision { get; set; }

    /// <summary>
    /// Gets or sets when the question last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets whether the question is new.
    /// </summary>
    public bool IsNew => string.IsNullOrEmpty(QuestionId);
}
