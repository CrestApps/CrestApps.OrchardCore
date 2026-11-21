using System.Text.Json.Serialization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// Describes a registered question type (control).
/// </summary>
public sealed class SurveyControlDescriptor
{
    /// <summary>
    /// Gets or sets the unique name, an open string such as SingleChoice or Acme.Signature.
    /// </summary>
    public string Name { get; set; }

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
    /// Gets or sets the picker category (see <see cref="SurveyControlCategories"/>).
    /// </summary>
    public string Category { get; set; } = SurveyControlCategories.Advanced;

    /// <summary>
    /// Gets or sets the Font Awesome class of the picker icon.
    /// </summary>
    public string Icon { get; set; } = "fa-solid fa-question";

    /// <summary>
    /// Gets or sets the position in its category.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets what the type can do.
    /// </summary>
    public SurveyControlCapabilities Capabilities { get; set; } = SurveyControlCapabilities.Answerable;

    /// <summary>
    /// Gets or sets the type of the settings stored in <c>SurveyQuestion.Settings</c>, or <see langword="null"/>.
    /// </summary>
    [JsonIgnore]
    public Type SettingsType { get; set; }

    /// <summary>
    /// Gets or sets the type of the handler.
    /// </summary>
    [JsonIgnore]
    public Type HandlerType { get; set; }

    /// <summary>
    /// Gets whether the type has a capability.
    /// </summary>
    /// <param name="capability">The capability.</param>
    /// <returns><see langword="true"/> when it has it.</returns>
    public bool Has(SurveyControlCapabilities capability)
        => (Capabilities & capability) == capability;
}
