using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The question editor side panel.
/// </summary>
public class SurveyQuestionEditorViewModel
{
    /// <summary>
    /// Gets or sets the survey id.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the question id.
    /// </summary>
    public string QuestionId { get; set; }

    /// <summary>
    /// Gets or sets the name shown of the question type.
    /// </summary>
    public string ControlName { get; set; }

    /// <summary>
    /// Gets or sets the editor shape.
    /// </summary>
    public IShape Editor { get; set; }

    /// <summary>
    /// Gets or sets whether the question was just saved.
    /// </summary>
    public bool Saved { get; set; }

    /// <summary>
    /// Gets or sets whether the question type is not available.
    /// </summary>
    public bool Unavailable { get; set; }
}
