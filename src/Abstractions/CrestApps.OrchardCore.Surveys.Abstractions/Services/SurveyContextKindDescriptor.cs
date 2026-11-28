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

    /// <summary>
    /// Gets or sets whether a record of this kind can point to one person, such as an enrollment. An anonymous response
    /// keeps only the kind of such a context, never its id. A kind shared by many respondents, such as a course, sets it
    /// to <see langword="false"/> and keeps its id. Unregistered kinds count as identifying.
    /// </summary>
    public bool IdentifiesRespondent { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the module that owns this kind grades its responses on its own page, such as Learning's
    /// grading page for quiz attempts. The Surveys grading queue then leaves these responses out, so each one waits in a
    /// single place. A grader can still open a response's grading page from the owner's page.
    /// </summary>
    public bool HasOwnGradingQueue { get; set; }
}
