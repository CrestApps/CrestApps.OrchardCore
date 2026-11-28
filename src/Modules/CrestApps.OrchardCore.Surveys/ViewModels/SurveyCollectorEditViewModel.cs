using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The collector editor.
/// </summary>
public class SurveyCollectorEditViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    [BindNever]
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the collector id, or empty for a new one.
    /// </summary>
    public string CollectorId { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    [Required]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets when it opens, in local time.
    /// </summary>
    public DateTime? Opens { get; set; }

    /// <summary>
    /// Gets or sets when it closes, in local time.
    /// </summary>
    public DateTime? Closes { get; set; }

    /// <summary>
    /// Gets or sets the most completed responses.
    /// </summary>
    public int? MaxResponses { get; set; }

    /// <summary>
    /// Gets or sets a new password, or empty to keep the current one.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets whether the password is removed.
    /// </summary>
    public bool RemovePassword { get; set; }

    /// <summary>
    /// Gets or sets the hidden variable values, one name=value per line.
    /// </summary>
    public string Variables { get; set; }

    /// <summary>
    /// Gets or sets the culture.
    /// </summary>
    public string Culture { get; set; }

    /// <summary>
    /// Gets or sets how long a kiosk shows the ending, in seconds.
    /// </summary>
    public int KioskResetSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether a password is saved.
    /// </summary>
    [BindNever]
    public bool HasPassword { get; set; }

    /// <summary>
    /// Gets or sets the public link.
    /// </summary>
    [BindNever]
    public string Link { get; set; }

    /// <summary>
    /// Gets or sets the collector, when it exists.
    /// </summary>
    [BindNever]
    public SurveyCollector Collector { get; set; }
}
