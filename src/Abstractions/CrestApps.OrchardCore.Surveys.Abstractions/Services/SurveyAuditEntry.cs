namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// What a Surveys audit trail event records.
/// </summary>
public sealed class SurveyAuditEntry
{
    /// <summary>
    /// Gets or sets the id of the user who acted, or a system name such as Workflow.
    /// </summary>
    public string ActorId { get; set; }

    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the survey.
    /// </summary>
    public string SurveyName { get; set; }

    /// <summary>
    /// Gets or sets the id of the response, or <see langword="null"/>.
    /// </summary>
    public string ResponseId { get; set; }

    /// <summary>
    /// Gets or sets the readable number of the response, or <see langword="null"/>.
    /// </summary>
    public string ResponseNumber { get; set; }

    /// <summary>
    /// Gets or sets the version number concerned, or <see langword="null"/>.
    /// </summary>
    public int? VersionNumber { get; set; }

    /// <summary>
    /// Gets or sets the score before a change, or <see langword="null"/>.
    /// </summary>
    public decimal? From { get; set; }

    /// <summary>
    /// Gets or sets the score after a change, or <see langword="null"/>.
    /// </summary>
    public decimal? To { get; set; }

    /// <summary>
    /// Gets or sets why, or <see langword="null"/>.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets more detail, such as how many responses were exported.
    /// </summary>
    public string Detail { get; set; }
}
