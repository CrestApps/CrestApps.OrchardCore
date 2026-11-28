namespace CrestApps.OrchardCore.Surveys.AuditTrail;

/// <summary>
/// What a Surveys audit trail event keeps.
/// </summary>
public sealed class SurveyAuditData
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the survey.
    /// </summary>
    public string SurveyName { get; set; }

    /// <summary>
    /// Gets or sets the id of the response.
    /// </summary>
    public string ResponseId { get; set; }

    /// <summary>
    /// Gets or sets the readable number of the response.
    /// </summary>
    public string ResponseNumber { get; set; }

    /// <summary>
    /// Gets or sets the version number.
    /// </summary>
    public int? VersionNumber { get; set; }

    /// <summary>
    /// Gets or sets the score before.
    /// </summary>
    public decimal? From { get; set; }

    /// <summary>
    /// Gets or sets the score after.
    /// </summary>
    public decimal? To { get; set; }

    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets more detail.
    /// </summary>
    public string Detail { get; set; }
}
