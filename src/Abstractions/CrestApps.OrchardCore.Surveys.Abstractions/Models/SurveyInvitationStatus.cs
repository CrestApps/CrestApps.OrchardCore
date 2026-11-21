namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// Where an invitation stands.
/// </summary>
public enum SurveyInvitationStatus
{
    /// <summary>
    /// Not sent yet.
    /// </summary>
    Pending,

    /// <summary>
    /// Sent.
    /// </summary>
    Sent,

    /// <summary>
    /// The link was opened.
    /// </summary>
    Opened,

    /// <summary>
    /// A response started.
    /// </summary>
    Started,

    /// <summary>
    /// The response was completed.
    /// </summary>
    Completed,

    /// <summary>
    /// Delivery failed.
    /// </summary>
    Bounced,

    /// <summary>
    /// The recipient does not want to be contacted.
    /// </summary>
    OptedOut,

    /// <summary>
    /// The link expired.
    /// </summary>
    Expired,
}
