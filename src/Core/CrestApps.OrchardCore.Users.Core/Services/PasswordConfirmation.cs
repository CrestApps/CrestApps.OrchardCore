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

    private const string Purpose = "CrestApps.OrchardCore.Users.PasswordConfirmation";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITimeLimitedDataProtector _protector;
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

        try
        {
            return Task.FromResult(string.Equals(_protector.Unprotect(cookie), userId, StringComparison.Ordinal));
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // Expired, from another site, or changed.
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public Task ConfirmAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId) || _httpContextAccessor.HttpContext is not { } httpContext)
        {
            return Task.CompletedTask;
        }

        httpContext.Response.Cookies.Append(CookieName, _protector.Protect(userId, Window), new CookieOptions
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Path = string.IsNullOrEmpty(httpContext.Request.PathBase) ? "/" : httpContext.Request.PathBase.Value,
            MaxAge = Window,
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public string GetConfirmationUrl(string returnUrl)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        return httpContext is null
            ? null
            : _linkGenerator.GetPathByAction(httpContext, "Index", "PasswordConfirmation", new { area = UsersConstants.Feature.Area, returnUrl });
    }
}
