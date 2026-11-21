using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Who is responding, as far as the request tells.
/// </summary>
public sealed class SurveyRespondentIdentity
{
    /// <summary>
    /// Gets or sets the id of the signed-in user, or <see langword="null"/>.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the party responding, or <see langword="null"/>.
    /// </summary>
    public PartyRef? Party { get; set; }

    /// <summary>
    /// Gets or sets the resume token from the cookie or the link, or <see langword="null"/>.
    /// </summary>
    public string ResumeToken { get; set; }

    /// <summary>
    /// Gets or sets the browser key from the uniqueness cookie, or <see langword="null"/>.
    /// </summary>
    public string BrowserKey { get; set; }

    /// <summary>
    /// Gets or sets the IP address, or <see langword="null"/>.
    /// </summary>
    public string IpAddress { get; set; }

    /// <summary>
    /// Gets or sets whether the CAPTCHA was passed for this request.
    /// </summary>
    public bool CaptchaPassed { get; set; }

    /// <summary>
    /// Gets or sets whether the caller is staff acting on behalf of the respondent (preview, kiosk).
    /// </summary>
    public bool IsStaff { get; set; }
}
