using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Conditions;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// One question of a survey, of a registered control type. Its scoring key never reaches respondents.
/// </summary>
public sealed class SurveyQuestion
{
    /// <summary>
    /// Gets or sets the stable id; answers are keyed by it and it never changes.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the variable code, unique within the survey, used by piping, formulas and exports.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name of the registered control, such as SingleChoice.
    /// </summary>
    public string ControlType { get; set; }

    /// <summary>
    /// Gets or sets the question text; supports piping.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the sanitized HTML shown under the title.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the placeholder of text inputs.
    /// </summary>
    public string Placeholder { get; set; }

    /// <summary>
    /// Gets or sets whether an answer is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets whether the question keeps its place when the page shuffles.
    /// </summary>
    public bool Pinned { get; set; }

    /// <summary>
    /// Gets or sets the id of the pool on the page the question belongs to, or null.
    /// </summary>
    public string PoolId { get; set; }

    /// <summary>
    /// Gets or sets when the question shows.
    /// </summary>
    public SurveyVisibility Visibility { get; set; } = new();

    /// <summary>
    /// Gets or sets when an answer is required; overrides Required when set.
    /// </summary>
    public ConditionRule RequiredWhen { get; set; }

    /// <summary>
    /// Gets or sets when the question can be answered; a disabled question keeps its value read-only.
    /// </summary>
    public ConditionRule EnabledWhen { get; set; }

    /// <summary>
    /// Gets or sets the choices of choice-based controls (columns of a matrix).
    /// </summary>
    public List<SurveyChoice> Choices { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the choices show in a random order.
    /// </summary>
    public bool ShuffleChoices { get; set; }

    /// <summary>
    /// Gets or sets the id of an earlier question whose choices this question carries forward.
    /// </summary>
    public string ChoicesFrom { get; set; }

    /// <summary>
    /// Gets or sets which choices are carried forward.
    /// </summary>
    public ChoicesFromMode ChoicesFromMode { get; set; }

    /// <summary>
    /// Gets or sets the rows of a matrix, the prompts of a matching question or the items of a multiple text question.
    /// </summary>
    public List<SurveySubQuestion> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets the scoring key. It is never sent to respondents.
    /// </summary>
    public SurveyQuestionScoring Scoring { get; set; } = new();

    /// <summary>
    /// Gets or sets the validation rules of the answer.
    /// </summary>
    public List<SurveyValidationRule> Validation { get; set; } = [];

    /// <summary>
    /// Gets or sets the control-specific settings.
    /// </summary>
    public JsonObject Settings { get; set; } = [];

    /// <summary>
    /// Gets or sets the data of consumers, such as tags or a competency.
    /// </summary>
    public JsonObject Properties { get; set; } = [];
}
