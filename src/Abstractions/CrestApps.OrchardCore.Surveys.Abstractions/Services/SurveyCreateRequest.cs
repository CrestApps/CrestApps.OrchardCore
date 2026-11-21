using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// What to create a survey from.
/// </summary>
public sealed class SurveyCreateRequest
{
    /// <summary>
    /// Gets or sets the technical name, or <see langword="null"/> to derive one from the title.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public string Kind { get; set; } = SurveyKinds.Survey;

    /// <summary>
    /// Gets or sets the name of the template, or <see langword="null"/> for a blank survey.
    /// </summary>
    public string Template { get; set; }

    /// <summary>
    /// Gets or sets a definition to start from instead of a template.
    /// </summary>
    public SurveyDefinition Definition { get; set; }

    /// <summary>
    /// Gets or sets the record that owns the survey, or <see langword="null"/>.
    /// </summary>
    public RecordRef? Owner { get; set; }
}
