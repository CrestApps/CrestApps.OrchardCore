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
            var claimsIdentity = _identityOptions.Value.ClaimsIdentity;
            var userId = context.Principal?.FindFirstValue(claimsIdentity.UserIdClaimType);
            var securityStamp = context.Principal?.FindFirstValue(claimsIdentity.SecurityStampClaimType);

            var check = await _resets.CheckAsync(userId, context.Properties.IssuedUtc, securityStamp, context.HttpContext.RequestAborted);

            if (check == CredentialsResetCheck.Refuse)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(context.Scheme.Name);

                return;
            }

            if (validatePrincipal is null)
            {
                return;
            }

            if (check == CredentialsResetCheck.Refresh)
            {
                await RunStampCheckNowAsync(context, validatePrincipal);

                return;
            }

            await validatePrincipal(context);
        };
    }

    // Identity's stamp check runs when the cookie has no issue time, and then, the stamp still matching, builds the
    // session's claims again from the account (with Orchard Core's OnRefreshingPrincipal) and renews the cookie. The issue
    // time is put back afterwards so the cookie handler can renew it: a renewed cookie is issued now, after the refresh.
    private static async Task RunStampCheckNowAsync(CookieValidatePrincipalContext context, Func<CookieValidatePrincipalContext, Task> validatePrincipal)
    {
        var issuedUtc = context.Properties.IssuedUtc;
        context.Properties.IssuedUtc = null;

        try
        {
            await validatePrincipal(context);
        }
        finally
        {
            context.Properties.IssuedUtc ??= issuedUtc;
        }
    }
}
