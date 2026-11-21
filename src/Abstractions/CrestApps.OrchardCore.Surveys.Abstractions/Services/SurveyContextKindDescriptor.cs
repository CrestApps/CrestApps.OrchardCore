using System.Text.Json.Serialization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Describes a kind of record responses may belong to, such as an enrollment, so filters can name it.
/// </summary>
public sealed class SurveyContextKindDescriptor
{
    /// <summary>
    /// Gets or sets the record kind, such as LearningEnrollment.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the name shown.
    /// </summary>
    [JsonIgnore]
    public LocalizationSource DisplayName { get; set; }
}
