using CrestApps.Core.Models;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// A way of collecting responses: a link, an invitation list, an embed or a kiosk, with its own window and limits.
/// </summary>
public sealed class SurveyCollector : CatalogItem, IAuditedModel
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the name staff see.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the kind, an open string (see SurveyCollectorKinds).
    /// </summary>
    public string Kind { get; set; } = SurveyCollectorKinds.WebLink;

    /// <summary>
    /// Gets or sets the random token of the public link /s/{token}.
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets whether the collector accepts responses.
    /// </summary>
    public CollectorStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when it opens, in UTC, or null.
    /// </summary>
    public DateTime? OpensUtc { get; set; }

    /// <summary>
    /// Gets or sets when it closes, in UTC, or null.
    /// </summary>
    public DateTime? ClosesUtc { get; set; }

    /// <summary>
    /// Gets or sets the most completed responses it accepts, or null.
    /// </summary>
    public int? MaxResponses { get; set; }

    /// <summary>
    /// Gets or sets the hash of the password respondents must enter, or null. It is never exported in plain form.
    /// </summary>
    public string PasswordHash { get; set; }

    /// <summary>
    /// Gets or sets the hidden variable values this collector sets.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = [];

    /// <summary>
    /// Gets or sets the culture the collector presents the survey in, or null for the request culture.
    /// </summary>
    public string Culture { get; set; }

    /// <summary>
    /// Gets or sets whether this is the survey's default link, created on first publish.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets how long a kiosk shows the ending before it returns to the landing page.
    /// </summary>
    public int KioskResetSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets when it was created, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who owns it.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the user name of the user who created it.
    /// </summary>
    public string Author { get; set; }
}
