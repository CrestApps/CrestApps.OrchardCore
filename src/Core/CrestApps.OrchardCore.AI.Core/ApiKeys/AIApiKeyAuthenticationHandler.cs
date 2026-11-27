using System.Security.Claims;
using System.Text.Encodings.Web;
using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.AI.Core.ApiKeys;

/// <summary>
/// Signs a request in with an API key from the <c>X-Api-Key</c> header or a bearer token that is a key.
/// </summary>
public sealed class AIApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IAIApiKeyStore _store;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIApiKeyAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="options">The scheme options.</param>
    /// <param name="logger">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    /// <param name="store">The key store.</param>
    /// <param name="clock">The clock.</param>
    public AIApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IAIApiKeyStore store,
        IClock clock)
        : base(options, logger, encoder)
    {
        _store = store;
        _clock = clock;
    }

    /// <inheritdoc/>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var key = ReadKey();
        var prefix = AIApiKeys.GetPrefix(key);

        if (prefix is null)
        {
            return AuthenticateResult.NoResult();
        }

        var stored = await _store.FindByPrefixAsync(prefix, Context.RequestAborted);
        var now = _clock.UtcNow;

        if (!AIApiKeys.Matches(stored, key, now))
        {
            return AuthenticateResult.Fail("The API key is not valid.");
        }

        if (!AIApiKeys.AllowsOrigin(stored, Request.Headers.Origin))
        {
            return AuthenticateResult.Fail("The API key may not be used from this web site.");
        }

        // Kept to the hour, so a busy key does not write on every call.
        if (stored.LastUsedUtc is not { } lastUsed || now - lastUsed >= TimeSpan.FromHours(1))
        {
            stored.LastUsedUtc = now;
            await _store.UpdateAsync(stored, Context.RequestAborted);
        }

        var principal = AIApiKeys.CreatePrincipal(stored);

        await AddRolePermissionsAsync(principal, stored);

        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    // Orchard Core grants a user the permissions of their roles from the permission claims it signs them in with, not
    // from their role claims, so a key gets its roles' permissions the same way, as OpenID client credentials do.
    private async Task AddRolePermissionsAsync(ClaimsPrincipal principal, AIApiKey stored)
    {
        if (stored.Kind == AIApiKeyKind.Browser ||
            principal.Identity is not ClaimsIdentity identity ||
            Context.RequestServices?.GetService<IRoleService>() is not { } roleService)
        {
            return;
        }

        foreach (var role in stored.Roles.Where(role => !string.IsNullOrEmpty(role)))
        {
            identity.AddClaims(await roleService.GetRoleClaimsAsync(role, Context.RequestAborted));
        }
    }

    private string ReadKey()
    {
        var header = Request.Headers[AIApiKeys.HeaderName].ToString();

        if (!string.IsNullOrEmpty(header))
        {
            return header.Trim();
        }

        var authorization = Request.Headers[HeaderNames.Authorization].ToString();

        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..].Trim()
            : null;
    }
}

/// <summary>
/// Lets a browser key chat with the profiles it names, which it has no role to do.
/// </summary>
public sealed class AIApiKeyProfileAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.Resource is AIProfile profile &&
            requirement.Permission.Name == AIPermissions.QueryAnyAIProfile.Name &&
            AIApiKeys.BrowserKeyAllowsProfile(context.User, profile.ItemId) &&
            AIApiKeys.AllowsScope(context.User, AIApiKeys.Scopes.Chat))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
