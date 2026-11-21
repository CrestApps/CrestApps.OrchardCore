namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// An operation on a survey that resource-based checks decide.
/// </summary>
public enum SurveyOperation
{
    /// <summary>
    /// Edit the draft and settings.
    /// </summary>
    Manage,

    /// <summary>
    /// Publish, restore and close.
    /// </summary>
    Publish,

    /// <summary>
    /// See responses and reports.
    /// </summary>
    ViewResponses,

    /// <summary>
    /// Export responses with identity.
    /// </summary>
    ExportResponses,

    /// <summary>
    /// Void and delete responses.
    /// </summary>
    DeleteResponses,

    /// <summary>
    /// Grade responses.
    /// </summary>
    Grade,

    /// <summary>
    /// Grant accommodations.
    /// </summary>
    ManageOverrides,

    /// <summary>
    /// Send invitations.
    /// </summary>
    ManageInvitations,
}
