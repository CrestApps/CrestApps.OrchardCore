using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Chains the delegated access checks onto the application cookie of a child tenant: each request validates a
/// delegated session with the parent, and signing out ends it.
/// </summary>
internal sealed class ChildCookieEventsSetup : IPostConfigureOptions<CookieAuthenticationOptions>
{
    /// <inheritdoc/>
    public void PostConfigure(string name, CookieAuthenticationOptions options)
    {
        if (name != IdentityConstants.ApplicationScheme)
        {
            return;
        }

        options.Events ??= new CookieAuthenticationEvents();

        var validatePrincipal = options.Events.OnValidatePrincipal;
        var signingOut = options.Events.OnSigningOut;

        options.Events.OnValidatePrincipal = async context =>
        {
            await validatePrincipal(context);

            if (context.Principal is not null)
            {
                await context.HttpContext.RequestServices
                    .GetRequiredService<DelegatedSessionPrincipalValidator>()
                    .ValidateAsync(context);
            }
        };

        options.Events.OnSigningOut = async context =>
        {
            var sessionId = DelegatedAccessClaims.GetSessionId(context.HttpContext.User);

            if (!string.IsNullOrEmpty(sessionId))
            {
                var services = context.HttpContext.RequestServices;

                try
                {
                    services.GetRequiredService<DelegatedSessionPrincipalValidator>().Forget(sessionId);
                    await services.GetRequiredService<ITenantHierarchyBroker>().EndSessionAsync(sessionId, DelegatedSessionRules.ChildSignOut);
                }
                catch (Exception ex) when (!ex.IsFatal())
                {
                    services.GetRequiredService<ILogger<ChildCookieEventsSetup>>()
                        .LogWarning(ex, "The delegated access session could not be ended with the parent tenant.");
                }
            }

            await signingOut(context);
        };
    }
}
