using System.Text.Encodings.Web;
using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using OrchardCore.Modules;
using OrchardCore.Security;

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

        return AuthenticateResult.Success(new AuthenticationTicket(AIApiKeys.CreatePrincipal(stored), Scheme.Name));
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
