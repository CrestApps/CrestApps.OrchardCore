using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// What a respondent may see of a question: texts piped and translated, choices in plan order, no key.
/// </summary>
public sealed class SurveyQuestionView
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the variable code.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the question type.
    /// </summary>
    public string ControlType { get; set; }

    /// <summary>
    /// Gets or sets the number shown, or <see langword="null"/>.
    /// </summary>
    public int? Number { get; set; }

    /// <summary>
    /// Gets or sets the piped title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the piped description (sanitized HTML).
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the placeholder.
    /// </summary>
    public string Placeholder { get; set; }

    /// <summary>
    /// Gets or sets whether an answer is required now.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets whether the question is read-only now.
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the settings respondents may see.
    /// </summary>
    public JsonObject Settings { get; set; } = [];

    /// <summary>
    /// Gets or sets the visible choices in plan order.
    /// </summary>
    public List<SurveyChoiceView> Choices { get; set; } = [];

    /// <summary>
    /// Gets or sets the rows that are not retired.
    /// </summary>
    public List<SurveySubQuestionView> Rows { get; set; } = [];
}
