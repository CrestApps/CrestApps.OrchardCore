using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.ViewModels;

/// <summary>
/// The model of the <c>SurveyControl</c> shape that a question type's Respond, Review, Print and Summary templates
/// render. It holds only what respondents may see.
/// </summary>
public class SurveyControlViewModel
{
    /// <summary>
    /// Gets or sets the respondent view of the question.
    /// </summary>
    public SurveyQuestionView Question { get; set; }

    /// <summary>
    /// Gets or sets the settings respondents may see.
    /// </summary>
    public JsonObject Settings { get; set; } = [];

    /// <summary>
    /// Gets or sets the answer, or <see langword="null"/>.
    /// </summary>
    public SurveyAnswer Answer { get; set; }

    /// <summary>
    /// Gets or sets the input name prefix: <c>q.{questionId}</c>.
    /// </summary>
    public string Prefix { get; set; }

    /// <summary>
    /// Gets or sets the choices in plan order, filtered by choice visibility.
    /// </summary>
    public IReadOnlyList<SurveyChoiceView> Choices { get; set; } = [];

    /// <summary>
    /// Gets or sets the errors of the answer.
    /// </summary>
    public IReadOnlyList<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the control is read-only.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Gets or sets the feedback the review settings allow, or <see langword="null"/>.
    /// </summary>
    public SurveyAnswerFeedback Feedback { get; set; }

    /// <summary>
    /// Gets or sets the culture of the response.
    /// </summary>
    public string Culture { get; set; }

    /// <summary>
    /// Reads the settings as a typed object.
    /// </summary>
    /// <typeparam name="TSettings">The settings type.</typeparam>
    /// <returns>The settings, or a new instance.</returns>
    public TSettings GetSettings<TSettings>()
        where TSettings : class, new()
        => Settings is null ? new TSettings() : Settings.Deserialize<TSettings>() ?? new TSettings();
}
