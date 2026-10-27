using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Telnyx.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Telnyx's answer to whether a user has a phone open: a live credential of theirs that a soft phone registered with
/// Telnyx from a window that is still open, and that no leg has since been refused on.
/// </summary>
public sealed class TelnyxAgentPhoneStatusProvider : IAgentPhoneStatusProvider
{
    private readonly ITelnyxAgentCredentialStore _credentialStore;
    private readonly IOptionsMonitor<TelnyxOptions> _options;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxAgentPhoneStatusProvider"/> class.
    /// </summary>
    /// <param name="credentialStore">The store of the credentials issued to the users' soft phones.</param>
    /// <param name="options">The Telnyx options.</param>
    /// <param name="clock">The clock.</param>
    public TelnyxAgentPhoneStatusProvider(
        ITelnyxAgentCredentialStore credentialStore,
        IOptionsMonitor<TelnyxOptions> options,
        IClock clock)
    {
        _credentialStore = credentialStore;
        _options = options;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<bool?> HasOpenPhoneAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || !_options.CurrentValue.IsConfigured)
        {
            return null;
        }

        var credentials = await _credentialStore.ListLiveByUserAsync(userId, _clock.UtcNow, cancellationToken);

        return credentials.Any(IsOpen);
    }

    // Registered from a window whose connection has not closed, and not refused as unavailable since it registered: a
    // phone that went away without its connection closing (the server restarted under it) is known only by the refusal.
    private static bool IsOpen(TelnyxAgentCredential credential)
        => credential.RegisteredUtc.HasValue &&
            !credential.ConnectionClosedUtc.HasValue &&
            !(credential.UnreachableUtc >= credential.RegisteredUtc);
}
