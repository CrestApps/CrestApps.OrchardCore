using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// A preset of a base question type, such as Net Promoter Score, a 0 to 10 rating with labels. A question made from a
/// preset is a question of the base type with prepared settings.
/// </summary>
public sealed class SurveyControlPresetDescriptor
{
    /// <summary>
    /// Gets or sets the unique name of the preset.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name of the base question type.
    /// </summary>
    public string BaseControl { get; set; }

    /// <summary>
    /// Gets or sets the name shown in the picker.
    /// </summary>
    [JsonIgnore]
    public LocalizationSource DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the description shown in the picker.
    /// </summary>
    [JsonIgnore]
    public LocalizationSource Description { get; set; }

    /// <summary>
    /// Gets or sets the picker category.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets the Font Awesome class of the picker icon.
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// Gets or sets the position in its category.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets what prepares a new question: its settings, choices and rows.
    /// </summary>
    [JsonIgnore]
    public Action<SurveyQuestion> Initialize { get; set; }
}
