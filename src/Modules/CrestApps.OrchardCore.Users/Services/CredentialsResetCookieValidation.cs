using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Refuses a sign-in cookie issued before the account's earlier credentials were reset (see
/// <see cref="UserCredentialsResets"/>), on every request, ahead of ASP.NET Core Identity's own security stamp check,
/// which runs only every <see cref="SecurityStampValidatorOptions.ValidationInterval"/>. It wraps the application
/// cookie's <see cref="CookieAuthenticationEvents.OnValidatePrincipal"/>, which <c>AddIdentity</c> sets to that check,
/// so the check still runs for every cookie this lets through.
/// </summary>
public sealed class CredentialsResetCookieValidation : IPostConfigureOptions<CookieAuthenticationOptions>
{
    private readonly UserCredentialsResets _resets;
    private readonly IOptions<IdentityOptions> _identityOptions;

    public CredentialsResetCookieValidation(UserCredentialsResets resets, IOptions<IdentityOptions> identityOptions)
    {
        _resets = resets;
        _identityOptions = identityOptions;
    }

    /// <inheritdoc/>
    public void PostConfigure(string name, CookieAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (name != IdentityConstants.ApplicationScheme)
        {
            return;
        }

        options.Events ??= new CookieAuthenticationEvents();

        var validatePrincipal = options.Events.OnValidatePrincipal;

        options.Events.OnValidatePrincipal = async context =>
        {
            var userId = context.Principal?.FindFirstValue(_identityOptions.Value.ClaimsIdentity.UserIdClaimType);

            if (await _resets.IsBeforeResetAsync(userId, context.Properties.IssuedUtc, context.HttpContext.RequestAborted))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(context.Scheme.Name);

                return;
            }

            if (validatePrincipal is not null)
            {
                await validatePrincipal(context);
            }
        };
    }
}
