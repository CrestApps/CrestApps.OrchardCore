using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// The "Confirm it's you" page (<c>EmailSignInConfirm</c>, see <see cref="Controllers.EmailOwnershipController"/>):
/// there for a signed-in user whose address isn't proved, on a site that allows local sign-in, when a code can confirm
/// their address (<see cref="EmailSignInEligibility.AllowsConfirmationAsync"/>).
/// </summary>
public sealed class EmailOwnershipConfirmationPage : IEmailOwnershipConfirmationPage
{
    /// <summary>
    /// The route name of the page.
    /// </summary>
    public const string RouteName = "EmailSignInConfirm";

    private readonly ISiteService _siteService;
    private readonly EmailSignInEligibility _eligibility;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EmailOwnershipConfirmationPage(
        ISiteService siteService,
        EmailSignInEligibility eligibility,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        _siteService = siteService;
        _eligibility = eligibility;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public async Task<string> GetUrlAsync(IUser user, string returnUrl = null, CancellationToken cancellationToken = default)
    {
        if (user is not User account ||
            string.IsNullOrWhiteSpace(account.Email) ||
            UserEmailOwnership.IsVerified(account.Properties, account.Email) ||
            _httpContextAccessor.HttpContext is not { } httpContext ||
            !await EmailSignInAvailability.IsAvailableAsync(_siteService) ||
            !await _eligibility.AllowsConfirmationAsync(account))
        {
            return null;
        }

        return _linkGenerator.GetPathByRouteValues(httpContext, RouteName, string.IsNullOrEmpty(returnUrl) ? null : new { returnUrl });
    }
}
