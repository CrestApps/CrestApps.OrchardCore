namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// How an invitation collector invites and reminds, kept in the collector's properties.
/// </summary>
public sealed class SurveyInvitationSettings
{
    /// <summary>
    /// Gets or sets the subject of the invitation, Liquid.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the message of the invitation, Liquid with the model Survey, Invitation and Link.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets after how many days an invitation link expires, or <see langword="null"/> for never.
    /// </summary>
    public int? ExpiresAfterDays { get; set; }

    /// <summary>
    /// Gets or sets the most reminders each recipient gets.
    /// </summary>
    public int ReminderCount { get; set; }

    /// <summary>
    /// Gets or sets how many days apart reminders are sent.
    /// </summary>
    public int ReminderEveryDays { get; set; } = 3;
}
