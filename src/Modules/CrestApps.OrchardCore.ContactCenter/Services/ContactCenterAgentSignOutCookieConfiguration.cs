using System.Security.Claims;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Chains Contact Center agent sign-out synchronization onto the application authentication cookie. Whenever a
/// user's cookie session ends — through an explicit log off, a programmatic or external front-channel sign-out,
/// or a security-stamp rejection — the agent's presence is set to signed-out and their soft-phone credentials
/// are revoked. Hooking the cookie handler's events replaces a tenant-wide middleware that only matched two
/// hardcoded account URLs, so a session that ends by any other means no longer leaves the agent falsely available
/// and their browser SIP credentials live. The agent-session cleanup background task remains the durable backstop
/// for pure cookie expiry, which never calls sign-out.
/// </summary>
internal sealed class ContactCenterAgentSignOutCookieConfiguration
    : IPostConfigureOptions<CookieAuthenticationOptions>
{
    private static readonly TimeSpan _synchronizationTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Chains the agent sign-out synchronization onto the application cookie scheme's <c>OnSigningOut</c> and
    /// <c>OnValidatePrincipal</c> events, preserving any handlers configured earlier so existing behavior is
    /// not replaced.
    /// </summary>
    /// <param name="name">The named options instance being configured.</param>
    /// <param name="options">The cookie authentication options to post-configure.</param>
    public void PostConfigure(string name, CookieAuthenticationOptions options)
    {
        if (!string.Equals(name, IdentityConstants.ApplicationScheme, StringComparison.Ordinal))
        {
            return;
        }

        var priorOnSigningOut = options.Events.OnSigningOut;

        options.Events.OnSigningOut = async context =>
        {
            if (priorOnSigningOut is not null)
            {
                await priorOnSigningOut(context);
            }

            var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            await SynchronizeAgentSignOutAsync(context.HttpContext, userId);
        };

        var priorOnValidatePrincipal = options.Events.OnValidatePrincipal;

        options.Events.OnValidatePrincipal = async context =>
        {
            var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (priorOnValidatePrincipal is not null)
            {
                await priorOnValidatePrincipal(context);
            }

            // A prior handler such as the security-stamp validator may reject the principal and sign it out.
            // That rejection happens inside cookie authentication, before HttpContext.User is populated, so the
            // OnSigningOut it triggers cannot see the user. Synchronize here using the id captured from the
            // principal before rejection, and only when the principal was actually rejected.
            if (!string.IsNullOrEmpty(userId) && context.Principal is null)
            {
                await SynchronizeAgentSignOutAsync(context.HttpContext, userId);
            }
        };
    }

    private static async Task SynchronizeAgentSignOutAsync(HttpContext httpContext, string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var services = httpContext.RequestServices;
        var logger = services.GetRequiredService<ILogger<ContactCenterAgentSignOutCookieConfiguration>>();

        // The cookie handler raises these events before it deletes the authentication cookie, so an exception or
        // cancellation escaping here would abort the sign-out and leave the user logged in. Isolate the
        // synchronization from the sign-out flow: never propagate, and use a bounded, request-independent token
        // so a client disconnect cannot cancel it. The agent-session cleanup background task is the backstop.
        using var timeout = new CancellationTokenSource(_synchronizationTimeout);

        try
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Synchronizing Contact Center agent sign-out for user '{UserId}'.",
                    userId.SanitizeLogValue());
            }

            var presenceManager = services.GetRequiredService<IAgentPresenceManager>();
            await presenceManager.SignOutAsync(userId, timeout.Token);

            var revokers = services.GetServices<ISoftPhoneCredentialRevoker>();
            await SoftPhoneCredentialRevocation.RevokeForUserAsync(revokers, userId, "signed-out", logger, timeout.Token);
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    "Contact Center agent sign-out synchronization failed for user '{UserId}'. Error type: {ErrorType}. The background cleanup task will reconcile the agent state.",
                    userId.SanitizeLogValue(),
                    ex.GetType().Name);
            }
        }
    }
}
