using System.Text.Json.Serialization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Describes a survey template.
/// </summary>
public sealed class SurveyTemplateDescriptor
{
    /// <summary>
    /// Gets or sets the unique name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name shown.
    /// </summary>
    [JsonIgnore]
    public LocalizationSource DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the description shown.
    /// </summary>
    [JsonIgnore]
    public LocalizationSource Description { get; set; }

    /// <summary>
    /// Gets or sets the kind of survey the template makes.
    /// </summary>
    public string Kind { get; set; } = SurveyKinds.Survey;

    /// <summary>
    /// Gets or sets the Font Awesome class of its icon.
    /// </summary>
    public string Icon { get; set; } = "fa-solid fa-clipboard-list";

    /// <summary>
    /// Gets or sets its position in the list.
    /// </summary>
    public int Position { get; set; }
}
