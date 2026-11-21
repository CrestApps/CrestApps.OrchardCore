namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// A change the designer outline makes to a draft.
/// </summary>
public sealed class SurveyDraftCommand
{
    /// <summary>
    /// Gets or sets the command: addPage, addQuestion, move, movePage, delete, deletePage, duplicate or duplicatePage.
    /// </summary>
    public string Command { get; set; }

    /// <summary>
    /// Gets or sets the page concerned.
    /// </summary>
    public string PageId { get; set; }

    /// <summary>
    /// Gets or sets the question concerned.
    /// </summary>
    public string QuestionId { get; set; }

    /// <summary>
    /// Gets or sets the question type of a new question.
    /// </summary>
    public string ControlType { get; set; }

    /// <summary>
    /// Gets or sets the preset of a new question, used instead of the question type.
    /// </summary>
    public string Preset { get; set; }

    /// <summary>
    /// Gets or sets the page a question moves to.
    /// </summary>
    public string ToPageId { get; set; }

    /// <summary>
    /// Gets or sets the position the item moves to or is added at, or <see langword="null"/> for the end.
    /// </summary>
    public int? Index { get; set; }
}
