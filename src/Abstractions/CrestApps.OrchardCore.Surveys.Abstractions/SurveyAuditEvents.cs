namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// The names of the audit trail events of Surveys, recorded through <see cref="Services.ISurveyAuditTrail"/>.
/// </summary>
public static class SurveyAuditEvents
{
    /// <summary>
    /// The audit trail category of Surveys.
    /// </summary>
    public const string Category = "Surveys";

    /// <summary>
    /// A survey was created.
    /// </summary>
    public const string SurveyCreated = "SurveyCreated";

    /// <summary>
    /// A survey version was published.
    /// </summary>
    public const string SurveyPublished = "SurveyPublished";

    /// <summary>
    /// An old version was copied into the draft.
    /// </summary>
    public const string SurveyRestored = "SurveyRestored";

    /// <summary>
    /// A survey was closed.
    /// </summary>
    public const string SurveyClosed = "SurveyClosed";

    /// <summary>
    /// A survey was archived.
    /// </summary>
    public const string SurveyArchived = "SurveyArchived";

    /// <summary>
    /// A survey was deleted.
    /// </summary>
    public const string SurveyDeleted = "SurveyDeleted";

    /// <summary>
    /// The access settings of a survey changed.
    /// </summary>
    public const string AccessChanged = "SurveyAccessChanged";

    /// <summary>
    /// Responses were exported.
    /// </summary>
    public const string ResponsesExported = "SurveyResponsesExported";

    /// <summary>
    /// Invitations (personal links and their recipients) were exported.
    /// </summary>
    public const string InvitationsExported = "SurveyInvitationsExported";

    /// <summary>
    /// Accommodations were exported.
    /// </summary>
    public const string OverridesExported = "SurveyOverridesExported";

    /// <summary>
    /// An export of responses, invitations or accommodations was refused: the user may not export survey responses.
    /// </summary>
    public const string ExportRefused = "SurveyExportRefused";

    /// <summary>
    /// A response was voided.
    /// </summary>
    public const string ResponseVoided = "SurveyResponseVoided";

    /// <summary>
    /// A response was deleted.
    /// </summary>
    public const string ResponseDeleted = "SurveyResponseDeleted";

    /// <summary>
    /// A completed response was reopened.
    /// </summary>
    public const string ResponseReopened = "SurveyResponseReopened";

    /// <summary>
    /// The score of a response changed.
    /// </summary>
    public const string ScoreChanged = "SurveyScoreChanged";

    /// <summary>
    /// An accommodation was granted.
    /// </summary>
    public const string OverrideGranted = "SurveyOverrideGranted";

    /// <summary>
    /// An accommodation was revoked.
    /// </summary>
    public const string OverrideRevoked = "SurveyOverrideRevoked";
}
