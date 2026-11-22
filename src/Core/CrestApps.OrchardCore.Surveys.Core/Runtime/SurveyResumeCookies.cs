using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Reads and writes the resume cookies of the respondent pages: one HttpOnly cookie <c>sr_{surveyId}</c> per survey on
/// the <c>/s</c> path, holding the resume token of the browser's response. A consumer that starts a response for
/// someone (a course, for example) calls <see cref="Write"/> instead of putting the token in a URL.
/// </summary>
/// <remarks>
/// Every resume cookie is bound to a generation cookie (<c>sr_g</c>, also on <c>/s</c>). Signing in or out writes a new
/// generation, so every resume cookie written before stops counting and is removed on the next respondent page, even
/// though the sign-in page cannot see cookies of the <c>/s</c> path.
/// </remarks>
public sealed class SurveyResumeCookies
{
    /// <summary>
    /// The prefix of the resume cookie names.
    /// </summary>
    public const string CookiePrefix = "sr_";

    /// <summary>
    /// The name of the generation cookie.
    /// </summary>
    public const string GenerationCookie = "sr_g";

    /// <summary>
    /// The path of the respondent pages, below the tenant's path base.
    /// </summary>
    public const string RespondentPath = "/s";

    private const char GenerationSeparator = '~';
    private const string GenerationItemKey = "CrestApps.Surveys.ResumeGeneration";

    private static readonly TimeSpan _resumeMaxAge = TimeSpan.FromDays(30);
    private static readonly TimeSpan _generationMaxAge = TimeSpan.FromDays(365);

    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResumeCookies"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public SurveyResumeCookies(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the name of the resume cookie of a survey.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <returns>The cookie name.</returns>
    public static string CookieName(string surveyId)
        => CookiePrefix + surveyId;

    /// <summary>
    /// Reads the resume token of a survey from the request. A cookie of an older generation (written before the last
    /// sign-in or sign-out) is removed and gives no token.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <returns>The token, or <see langword="null"/>.</returns>
    public string Read(string surveyId)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null || string.IsNullOrEmpty(surveyId) || !httpContext.Request.Cookies.TryGetValue(CookieName(surveyId), out var value))
        {
            return null;
        }

        var token = Parse(value, CurrentGeneration(httpContext));

        if (token is null)
        {
            Clear(surveyId);
        }

        return token;
    }

    /// <summary>
    /// Writes the resume cookie of a survey.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="token">The resume token.</param>
    public void Write(string surveyId, string token)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null || string.IsNullOrEmpty(surveyId) || string.IsNullOrEmpty(token))
        {
            return;
        }

        var generation = CurrentGeneration(httpContext);

        if (string.IsNullOrEmpty(generation))
        {
            generation = NewGeneration(httpContext);
        }

        httpContext.Response.Cookies.Append(CookieName(surveyId), Format(token, generation), Options(httpContext, _resumeMaxAge));
    }

    /// <summary>
    /// Removes the resume cookie of a survey.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    public void Clear(string surveyId)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is not null && !string.IsNullOrEmpty(surveyId))
        {
            httpContext.Response.Cookies.Delete(CookieName(surveyId), Options(httpContext, maxAge: null));
        }
    }

    /// <summary>
    /// Forgets every resume cookie of the browser: the ones the request carries are removed, and a new generation makes
    /// the others stop counting. Called when a user signs in or out.
    /// </summary>
    public void ClearAll()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            return;
        }

        foreach (var name in httpContext.Request.Cookies.Keys)
        {
            if (name.StartsWith(CookiePrefix, StringComparison.Ordinal) && !string.Equals(name, GenerationCookie, StringComparison.Ordinal))
            {
                httpContext.Response.Cookies.Delete(name, Options(httpContext, maxAge: null));
            }
        }

        NewGeneration(httpContext);
    }

    /// <summary>
    /// Formats the value of a resume cookie.
    /// </summary>
    /// <param name="token">The resume token.</param>
    /// <param name="generation">The generation.</param>
    /// <returns>The cookie value.</returns>
    public static string Format(string token, string generation)
        => token + GenerationSeparator + generation;

    /// <summary>
    /// Reads the resume token of a cookie value when it belongs to the current generation.
    /// </summary>
    /// <param name="value">The cookie value.</param>
    /// <param name="generation">The current generation, or <see langword="null"/> when there is none.</param>
    /// <returns>The token, or <see langword="null"/>.</returns>
    public static string Parse(string value, string generation)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(generation) || value.Length > 300)
        {
            return null;
        }

        var separator = value.LastIndexOf(GenerationSeparator);

        if (separator <= 0)
        {
            return null;
        }

        var stored = value.Substring(separator + 1);

        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(stored), Encoding.ASCII.GetBytes(generation))
            ? value.Substring(0, separator)
            : null;
    }

    /// <summary>
    /// Gets the cookie options of the respondent pages for a request: HttpOnly, SameSite Lax, on the tenant's
    /// <c>/s</c> path.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="maxAge">How long the cookie lives, or <see langword="null"/> to remove it.</param>
    /// <returns>The options.</returns>
    public static CookieOptions Options(HttpContext httpContext, TimeSpan? maxAge)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            MaxAge = maxAge,
            Path = httpContext.Request.PathBase.Add(RespondentPath).Value,
        };
    }

    private static string CurrentGeneration(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(GenerationItemKey, out var written) && written is string issued)
        {
            return issued;
        }

        return httpContext.Request.Cookies.TryGetValue(GenerationCookie, out var generation) && generation.Length <= 64 ? generation : null;
    }

    private static string NewGeneration(HttpContext httpContext)
    {
        var generation = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

        httpContext.Items[GenerationItemKey] = generation;
        httpContext.Response.Cookies.Append(GenerationCookie, generation, Options(httpContext, _generationMaxAge));

        return generation;
    }
}
