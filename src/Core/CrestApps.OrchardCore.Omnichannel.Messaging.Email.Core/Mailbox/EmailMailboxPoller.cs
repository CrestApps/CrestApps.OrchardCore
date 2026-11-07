using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Mailbox;

/// <summary>
/// The default <see cref="IEmailMailboxPoller"/>. Each mailbox is read under a lock of its own, so two nodes never read
/// the same mailbox at once, and a mailbox that keeps failing (a changed password, a server that is down) is tried less
/// and less often, up to once an hour, instead of every minute.
/// </summary>
public sealed class EmailMailboxPoller : IEmailMailboxPoller
{
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan _maxBackoff = TimeSpan.FromHours(1);

    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly ICatalog<EmailMailboxSyncState> _syncStates;
    private readonly IEmailMailboxReader _reader;
    private readonly IDistributedLock _distributedLock;
    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailMailboxPoller"/> class.
    /// </summary>
    /// <param name="endpointManager">The manager of the business's addresses.</param>
    /// <param name="syncStates">The catalog the read positions and failures are kept in.</param>
    /// <param name="reader">The mailbox reader.</param>
    /// <param name="distributedLock">The lock that keeps one reader per mailbox.</param>
    /// <param name="shellHost">The shell host the received emails are processed on.</param>
    /// <param name="shellSettings">The tenant's shell settings.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public EmailMailboxPoller(
        IOmnichannelChannelEndpointManager endpointManager,
        ICatalog<EmailMailboxSyncState> syncStates,
        IEmailMailboxReader reader,
        IDistributedLock distributedLock,
        IShellHost shellHost,
        ShellSettings shellSettings,
        IClock clock,
        ILogger<EmailMailboxPoller> logger)
    {
        _endpointManager = endpointManager;
        _syncStates = syncStates;
        _reader = reader;
        _distributedLock = distributedLock;
        _shellHost = shellHost;
        _shellSettings = shellSettings;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> PollAsync(CancellationToken cancellationToken = default)
    {
        var received = 0;

        foreach (var address in await _endpointManager.GetAllAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!address.HasCapability(OmnichannelConstants.Channels.Email) ||
                !address.TryGet<EmailAddressSettings>(out var settings) ||
                settings.InboundMode != EmailInboundMode.Mailbox ||
                settings.Mailbox?.Server?.IsConfigured != true)
            {
                continue;
            }

            var state = await _syncStates.FindByIdAsync(address.ItemId, cancellationToken);

            if (state is not null && !IsDue(state, _clock.UtcNow))
            {
                continue;
            }

            var (locker, locked) = await _distributedLock.TryAcquireLockAsync($"EmailMailbox:{address.ItemId}", TimeSpan.Zero, _lockExpiration);

            if (!locked)
            {
                // Another node is reading this mailbox right now.
                continue;
            }

            await using (locker)
            {
                try
                {
                    var result = await _reader.ReadAsync(address, settings, cancellationToken);

                    received += result.Received;

                    await EmailInboxBackgroundDispatch.DispatchAsync(_shellHost, _shellSettings, result.InboxMessageIds);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Reading the mailbox of email address '{AddressId}' failed unexpectedly.", address.ItemId.SanitizeLogValue());
                }
            }
        }

        return received;
    }

    /// <summary>
    /// Determines whether a mailbox is due to be read: always, unless its last reads failed, in which case it waits a
    /// doubling interval (two minutes after the first failure, up to an hour).
    /// </summary>
    /// <param name="state">The mailbox's read state.</param>
    /// <param name="utcNow">The current time.</param>
    /// <returns><see langword="true"/> when the mailbox should be read now.</returns>
    public static bool IsDue(EmailMailboxSyncState state, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.ConsecutiveFailures <= 0 || state.LastFailedUtc is null)
        {
            return true;
        }

        var minutes = Math.Pow(2, Math.Min(state.ConsecutiveFailures, 6));
        var backoff = TimeSpan.FromMinutes(Math.Min(minutes, _maxBackoff.TotalMinutes));

        return utcNow - state.LastFailedUtc.Value >= backoff;
    }
}
