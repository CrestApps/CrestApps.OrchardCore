using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.Users.Core.Services;

/// <summary>
/// The default <see cref="IPasswordConfirmation"/>: a confirmation is an HTTP-only cookie holding the person's user id,
/// protected with a data protector that stops reading it after <see cref="Window"/>, so it is tied to that person and
/// that browser and can't be extended or moved.
/// </summary>
public sealed class PasswordConfirmation : IPasswordConfirmation
{
    /// <summary>
    /// The cookie's name.
    /// </summary>
    public const string CookieName = "crestapps-password-confirmed";

    /// <summary>
    /// The name of the cookie that says the person confirmed for the next page they open (<see cref="IsJustConfirmedAsync"/>).
    /// </summary>
    public const string JustConfirmedCookieName = "crestapps-password-just-confirmed";

    private const string Purpose = "CrestApps.OrchardCore.Users.PasswordConfirmation";

    private const string JustConfirmedPurpose = Purpose + ".JustConfirmed";

    // Long enough for the redirect back to the page, short enough that it can't wait for someone else.
    private static readonly TimeSpan _justConfirmedWindow = TimeSpan.FromMinutes(1);

    private static readonly object _justConfirmedKey = new();

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITimeLimitedDataProtector _protector;
    private readonly ITimeLimitedDataProtector _justConfirmedProtector;
    private readonly LinkGenerator _linkGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordConfirmation"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor, for the cookie.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="linkGenerator">The link generator, for the confirmation page.</param>
    public PasswordConfirmation(IHttpContextAccessor httpContextAccessor, IDataProtectionProvider dataProtectionProvider, LinkGenerator linkGenerator)
    {
        _httpContextAccessor = httpContextAccessor;
        _protector = dataProtectionProvider.CreateProtector(Purpose).ToTimeLimitedDataProtector();
        _justConfirmedProtector = dataProtectionProvider.CreateProtector(JustConfirmedPurpose).ToTimeLimitedDataProtector();
        _linkGenerator = linkGenerator;
    }

    /// <inheritdoc />
    public TimeSpan Window { get; } = TimeSpan.FromMinutes(10);

    /// <inheritdoc />
    public Task<bool> IsConfirmedAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId) || _httpContextAccessor.HttpContext?.Request.Cookies[CookieName] is not { Length: > 0 } cookie)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(Holds(_protector, cookie, userId));
    }

    /// <inheritdoc />
    public Task<bool> IsJustConfirmedAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (_httpContextAccessor.HttpContext is not { } httpContext)
        {
            return Task.FromResult(false);
        }

        // Every pay card of the page asks; the first one uses the cookie up and the others get its answer.
        if (httpContext.Items.TryGetValue(_justConfirmedKey, out var known) && known is bool answer)
        {
            return Task.FromResult(answer);
        }

        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var justConfirmed = false;

        if (httpContext.Request.Cookies[JustConfirmedCookieName] is { Length: > 0 } cookie)
        {
            justConfirmed = !string.IsNullOrEmpty(userId) && Holds(_justConfirmedProtector, cookie, userId);

            // Used once: the next time the page opens it asks again.
            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.Cookies.Delete(JustConfirmedCookieName, Options(httpContext, null));
            }
        }

        httpContext.Items[_justConfirmedKey] = justConfirmed;

        return Task.FromResult(justConfirmed);
    }

    /// <inheritdoc />
    public Task ConfirmAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId) || _httpContextAccessor.HttpContext is not { } httpContext)
        {
            return Task.CompletedTask;
        }

        httpContext.Response.Cookies.Append(CookieName, _protector.Protect(userId, Window), Options(httpContext, Window));
        httpContext.Response.Cookies.Append(JustConfirmedCookieName, _justConfirmedProtector.Protect(userId, _justConfirmedWindow), Options(httpContext, _justConfirmedWindow));

        return Task.CompletedTask;
    }

    private static bool Holds(ITimeLimitedDataProtector protector, string cookie, string userId)
    {
        try
        {
            return string.Equals(protector.Unprotect(cookie), userId, StringComparison.Ordinal);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // Expired, from another site, or changed.
            return false;
        }
    }

    private static CookieOptions Options(HttpContext httpContext, TimeSpan? maxAge)
        => new()
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Path = string.IsNullOrEmpty(httpContext.Request.PathBase) ? "/" : httpContext.Request.PathBase.Value,
            MaxAge = maxAge,
        };

    /// <inheritdoc />
    public string GetConfirmationUrl(string returnUrl)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        return httpContext is null
            ? null
            : _linkGenerator.GetPathByAction(httpContext, "Index", "PasswordConfirmation", new { area = UsersConstants.Feature.Area, returnUrl });
    }
}
