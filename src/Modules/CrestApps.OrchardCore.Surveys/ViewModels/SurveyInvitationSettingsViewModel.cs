using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The settings of an invitation collector.
/// </summary>
public class SurveyInvitationSettingsViewModel
{
    /// <summary>
    /// Gets or sets the survey id.
    /// </summary>
    [BindNever]
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the survey title.
    /// </summary>
    [BindNever]
    public string SurveyTitle { get; set; }

    /// <summary>
    /// Gets or sets the collector id, or <see langword="null"/> for a new collector.
    /// </summary>
    [BindNever]
    public string CollectorId { get; set; }

    /// <summary>
    /// Gets or sets the name of the collector.
    /// </summary>
    [Required]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the subject, Liquid.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the message, Liquid.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets after how many days links expire.
    /// </summary>
    public int? ExpiresAfterDays { get; set; }

    /// <summary>
    /// Gets or sets the most reminders.
    /// </summary>
    public int ReminderCount { get; set; }

    /// <summary>
    /// Gets or sets how many days apart reminders are sent.
    /// </summary>
    public int ReminderEveryDays { get; set; } = 3;
}
