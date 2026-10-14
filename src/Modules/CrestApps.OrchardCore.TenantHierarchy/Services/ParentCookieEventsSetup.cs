using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Ends every delegated access session a parent sign-in started when the parent user signs out.
/// </summary>
internal sealed class ParentCookieEventsSetup : IPostConfigureOptions<CookieAuthenticationOptions>
{
    /// <inheritdoc/>
    public void PostConfigure(string name, CookieAuthenticationOptions options)
    {
        if (name != IdentityConstants.ApplicationScheme)
        {
            return;
        }

        options.Events ??= new CookieAuthenticationEvents();

        var signingOut = options.Events.OnSigningOut;

        options.Events.OnSigningOut = async context =>
        {
            var parentSessionId = context.HttpContext.User.FindFirst(TenantHierarchyConstants.ClaimTypes.ParentSessionId)?.Value;

            if (!string.IsNullOrEmpty(parentSessionId))
            {
                var services = context.HttpContext.RequestServices;

                try
                {
                    await services.GetRequiredService<DelegatedAccessIssuer>().EndSessionsOfSignInAsync(parentSessionId);
                }
                catch (Exception ex) when (!ex.IsFatal())
                {
                    services.GetRequiredService<ILogger<ParentCookieEventsSetup>>()
                        .LogWarning(ex, "The delegated access sessions of a parent sign-in could not be ended.");
                }
            }

            await signingOut(context);
        };
    }
}
