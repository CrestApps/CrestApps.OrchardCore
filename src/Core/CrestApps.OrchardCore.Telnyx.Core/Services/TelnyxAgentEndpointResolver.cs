using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telnyx.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Resolves the SIP address an agent's browser is actually listening on.
/// <para>
/// Two call paths — the Contact Center voice provider and extension dialling — each took the first live
/// credential the store returned, which is the newest **issued** one. A browser that had re-registered under an
/// older credential was therefore dialled at an address nothing was listening on, and the call came back SIP
/// 486: the agent's phone never rang and the caller heard busy. Registration, not recency, is what makes a
/// credential reachable, so both paths now go through here and the chosen credential is logged.
/// </para>
/// </summary>
public sealed class TelnyxAgentEndpointResolver : ITelnyxAgentEndpointResolver
{
    private readonly ITelnyxAgentCredentialStore _credentialStore;
    private readonly TelnyxOptions _options;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxAgentEndpointResolver"/> class.
    /// </summary>
    public TelnyxAgentEndpointResolver(
        ITelnyxAgentCredentialStore credentialStore,
        IOptions<TelnyxOptions> options,
        IClock clock,
        ILogger<TelnyxAgentEndpointResolver> logger)
    {
        _credentialStore = credentialStore;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<string> ResolveAsync(string userId, CancellationToken cancellationToken = default)
        => ResolveAsync(userId, requiredClientCapability: null, cancellationToken);

    /// <inheritdoc/>
    public async Task<string> ResolveAsync(string userId, string requiredClientCapability, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var live = await _credentialStore.ListLiveByUserAsync(userId.Trim(), _clock.UtcNow, cancellationToken);

        // Registered first, then most recently registered, then newest issued: the last of those only matters
        // when nothing has registered at all, where the newest is the one the browser is most likely registering
        // against right now.
        var credential = TelnyxAgentCredentialSelection.OrderByDeliveryPreference(live)
            .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate.SipUsername));

        if (credential is null)
        {
            // The caller must be able to tell "this agent has no phone" from "this agent has a phone at some
            // address"; inventing an address hides the first until the call fails.
            _logger.LogWarning(
                "No live Telnyx SIP credential with a username for user '{UserId}', so no agent endpoint can be resolved.",
                userId.SanitizeLogValue());

            return null;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Resolved Telnyx agent endpoint for user '{UserId}' to credential '{CredentialId}' (registered {RegisteredUtc:o}, issued {IssuedUtc:o}).",
                userId.SanitizeLogValue(),
                credential.CredentialId.SanitizeLogValue(),
                credential.RegisteredUtc,
                credential.IssuedUtc);
        }

        // The choice of credential is the same either way; a capability only decides whether that one will do. A
        // client that predates a capability never reported it, and is dialed the way it always was.
        if (!string.IsNullOrEmpty(requiredClientCapability) &&
            credential.ClientCapabilities?.Contains(requiredClientCapability, StringComparer.Ordinal) != true)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "The client registered on credential '{CredentialId}' for user '{UserId}' did not report '{Capability}'.",
                    credential.CredentialId.SanitizeLogValue(),
                    userId.SanitizeLogValue(),
                    requiredClientCapability.SanitizeLogValue());
            }

            return null;
        }

        var sipDomain = string.IsNullOrWhiteSpace(_options.SipDomain)
            ? TelnyxConstants.DefaultSipDomain
            : _options.SipDomain;

        return $"sip:{credential.SipUsername}@{sipDomain}";
    }
}
