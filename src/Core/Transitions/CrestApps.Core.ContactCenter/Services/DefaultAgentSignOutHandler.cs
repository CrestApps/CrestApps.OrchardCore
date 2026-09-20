using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;
using CrestApps.Core.Telephony;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Signs the agent out of presence and revokes their soft-phone credentials when their session ends.
/// </summary>
public sealed class DefaultAgentSignOutHandler : IAgentSignOutHandler
{
    /// <summary>
    /// How long the synchronisation is given before it is abandoned.
    /// </summary>
    /// <remarks>
    /// Bounded because this runs inside the sign-out pipeline: a slow presence store must not hold
    /// the user's log off open. The agent-session cleanup background task reconciles anything missed.
    /// </remarks>
    private static readonly TimeSpan _synchronizationTimeout = TimeSpan.FromSeconds(10);

    private const string RevocationReason = "signed-out";

    private readonly IAgentPresenceManager _presenceManager;
    private readonly IEnumerable<ISoftPhoneCredentialRevoker> _credentialRevokers;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAgentSignOutHandler"/> class.
    /// </summary>
    /// <param name="presenceManager">The presence manager that records the agent as signed out.</param>
    /// <param name="credentialRevokers">The revokers that invalidate the agent's soft-phone credentials.</param>
    /// <param name="timeProvider">The time provider that bounds the synchronisation.</param>
    /// <param name="logger">The logger.</param>
    public DefaultAgentSignOutHandler(
        IAgentPresenceManager presenceManager,
        IEnumerable<ISoftPhoneCredentialRevoker> credentialRevokers,
        TimeProvider timeProvider,
        ILogger<DefaultAgentSignOutHandler> logger)
    {
        _presenceManager = presenceManager;
        _credentialRevokers = credentialRevokers;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task HandleAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        // Bounded, and independent of any request. The host raises sign-out before it deletes the
        // authentication cookie, so a client that disconnects mid-request must not cancel this and
        // leave the agent available with live credentials.
        using var timeout = new CancellationTokenSource(_synchronizationTimeout, _timeProvider);

        try
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Synchronizing Contact Center agent sign-out for user '{UserId}'.",
                    userId.SanitizeLogValue());
            }

            await _presenceManager.SignOutAsync(userId, timeout.Token);

            await SoftPhoneCredentialRevocation.RevokeForUserAsync(
                _credentialRevokers,
                userId,
                RevocationReason,
                _logger,
                timeout.Token);
        }
        catch (Exception ex)
        {
            // Never propagates: an exception escaping here would abort the sign-out and leave the user
            // logged in. A missed synchronisation is recoverable, a failed sign-out is not.
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Contact Center agent sign-out synchronization failed for user '{UserId}'. Error type: {ErrorType}. The background cleanup task will reconcile the agent state.",
                    userId.SanitizeLogValue(),
                    ex.GetType().Name);
            }
        }
    }
}
