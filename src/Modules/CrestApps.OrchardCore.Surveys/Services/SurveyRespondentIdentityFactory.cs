using System.Security.Claims;
using System.Security.Cryptography;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Builds the respondent identity of a request: the signed-in user, the resume token from the survey's cookie, the
/// browser key cookie and the IP address; and writes the resume cookie.
/// </summary>
/// <remarks>
/// The resume token comes from the cookie only. A token on a resume link (<c>?t=</c>) is never read here: the runner
/// trades it for a new token and a cookie, then redirects to the address without it.
/// </remarks>
public sealed class SurveyRespondentIdentityFactory
{
    /// <summary>
    /// The name of the browser key cookie used for weak uniqueness on public links.
    /// </summary>
    public const string BrowserKeyCookie = "sb";

    /// <summary>
    /// The query string key of a resume token on a resume link.
    /// </summary>
    public const string ResumeQueryKey = "t";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SurveyResumeCookies _resumeCookies;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyRespondentIdentityFactory"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="resumeCookies">The resume cookies.</param>
    public SurveyRespondentIdentityFactory(
        IHttpContextAccessor httpContextAccessor,
        SurveyResumeCookies resumeCookies)
    {
        _httpContextAccessor = httpContextAccessor;
        _resumeCookies = resumeCookies;
    }

    /// <summary>
    /// Gets the name of the resume cookie of a survey.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <returns>The cookie name.</returns>
    public static string ResumeCookie(string surveyId)
        => SurveyResumeCookies.CookieName(surveyId);

    /// <summary>
    /// Builds the identity of the current request for a survey.
    /// </summary>
    /// <param name="surveyId">The survey, to read its resume cookie, or <see langword="null"/>.</param>
    /// <param name="ensureBrowserKey">Whether to issue a browser key cookie when there is none.</param>
    /// <returns>The identity.</returns>
    public SurveyRespondentIdentity Build(string surveyId, bool ensureBrowserKey = false)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var identity = new SurveyRespondentIdentity();

        if (httpContext is null)
        {
            return identity;
        }

        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            identity.UserId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(identity.UserId))
            {
                identity.Party = new PartyRef(PartyKinds.User, identity.UserId);
            }
        }

        identity.ResumeToken = _resumeCookies.Read(surveyId);

        if (httpContext.Request.Cookies.TryGetValue(BrowserKeyCookie, out var browserKey) && browserKey.Length <= 64)
        {
            identity.BrowserKey = browserKey;
        }
        else if (ensureBrowserKey)
        {
            identity.BrowserKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            httpContext.Response.Cookies.Append(BrowserKeyCookie, identity.BrowserKey, SurveyResumeCookies.Options(httpContext, TimeSpan.FromDays(365)));
        }

        identity.IpAddress = httpContext.Connection?.RemoteIpAddress?.ToString();

        return identity;
    }

    /// <summary>
    /// Writes the resume cookie of a survey.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="token">The resume token.</param>
    public void WriteResumeCookie(string surveyId, string token)
        => _resumeCookies.Write(surveyId, token);

    /// <summary>
    /// Removes the resume cookie of a survey, for kiosks.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    public void ClearResumeCookie(string surveyId)
        => _resumeCookies.Clear(surveyId);
}
