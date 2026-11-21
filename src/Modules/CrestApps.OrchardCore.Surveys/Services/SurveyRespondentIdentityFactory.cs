using System.Security.Claims;
using System.Security.Cryptography;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Builds the respondent identity of a request: the signed-in user, the resume token from the survey's cookie or the
/// link, the browser key cookie and the IP address; and writes the resume cookie.
/// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyRespondentIdentityFactory"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public SurveyRespondentIdentityFactory(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the name of the resume cookie of a survey.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <returns>The cookie name.</returns>
    public static string ResumeCookie(string surveyId)
        => "sr_" + surveyId;

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

        var query = httpContext.Request.Query[ResumeQueryKey].ToString();

        identity.ResumeToken = !string.IsNullOrEmpty(query)
            ? query
            : !string.IsNullOrEmpty(surveyId) && httpContext.Request.Cookies.TryGetValue(ResumeCookie(surveyId), out var cookie) ? cookie : null;

        if (httpContext.Request.Cookies.TryGetValue(BrowserKeyCookie, out var browserKey) && browserKey.Length <= 64)
        {
            identity.BrowserKey = browserKey;
        }
        else if (ensureBrowserKey)
        {
            identity.BrowserKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            httpContext.Response.Cookies.Append(BrowserKeyCookie, identity.BrowserKey, new CookieOptions
            {
                HttpOnly = true,
                Secure = httpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                MaxAge = TimeSpan.FromDays(365),
                Path = "/s",
            });
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
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null || string.IsNullOrEmpty(surveyId) || string.IsNullOrEmpty(token))
        {
            return;
        }

        httpContext.Response.Cookies.Append(ResumeCookie(surveyId), token, new CookieOptions
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            MaxAge = TimeSpan.FromDays(30),
            Path = "/s",
        });
    }

    /// <summary>
    /// Removes the resume cookie of a survey, for kiosks.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    public void ClearResumeCookie(string surveyId)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is not null && !string.IsNullOrEmpty(surveyId))
        {
            httpContext.Response.Cookies.Delete(ResumeCookie(surveyId), new CookieOptions { Path = "/s" });
        }
    }
}
