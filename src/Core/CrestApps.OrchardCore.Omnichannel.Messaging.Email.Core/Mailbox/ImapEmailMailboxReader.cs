using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Mailbox;

/// <summary>
/// Reads an address's mailbox over IMAP. Each read takes the mail whose UID is above the last one received, in order,
/// hands it to the inbound receiver, then marks it read (and moves it, when the address says so). The position is kept per
/// address with the folder's <c>UIDVALIDITY</c>, so a renumbered folder starts again from the newest mail rather than
/// receiving everything twice, and a redelivered email is absorbed by the inbox's <c>Message-ID</c> check.
/// </summary>
public sealed class ImapEmailMailboxReader : IEmailMailboxReader
{
    /// <summary>
    /// The most emails one read takes, so a backlog is worked through over several passes rather than in one long one.
    /// </summary>
    public const int MaxMessagesPerRead = 50;

    private readonly IEmailInboundReceiver _receiver;
    private readonly ICatalog<EmailMailboxSyncState> _syncStates;
    private readonly IEmailSecretProtector _secretProtector;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImapEmailMailboxReader"/> class.
    /// </summary>
    /// <param name="receiver">The inbound receiver each email is handed to.</param>
    /// <param name="syncStates">The catalog the read positions are kept in.</param>
    /// <param name="secretProtector">The protector the mailbox password is read with.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public ImapEmailMailboxReader(
        IEmailInboundReceiver receiver,
        ICatalog<EmailMailboxSyncState> syncStates,
        IEmailSecretProtector secretProtector,
        IClock clock,
        ILogger<ImapEmailMailboxReader> logger)
    {
        _receiver = receiver;
        _syncStates = syncStates;
        _secretProtector = secretProtector;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<EmailMailboxReadResult> ReadAsync(OmnichannelChannelEndpoint address, EmailAddressSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(settings);

        var mailbox = settings.Mailbox ?? new EmailMailboxSettings();
        var server = mailbox.Server ?? new EmailServerSettings();

        if (!server.IsConfigured)
        {
            return new EmailMailboxReadResult { Error = "The address reads its mailbox, but no IMAP server is set on the address." };
        }

        var folderName = string.IsNullOrWhiteSpace(mailbox.Folder) ? EmailMailboxSettings.DefaultFolder : mailbox.Folder.Trim();
        var existing = await _syncStates.FindByIdAsync(address.ItemId, cancellationToken);
        var state = existing ?? new EmailMailboxSyncState { ItemId = address.ItemId };

        // A mailbox pointed at another server or folder is a different list of mail with its own numbering.
        if (!string.Equals(state.Host, server.Host, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(state.Folder, folderName, StringComparison.OrdinalIgnoreCase))
        {
            state.Host = server.Host;
            state.Folder = folderName;
            state.UidValidity = 0;
            state.LastUid = 0;
        }

        var result = new EmailMailboxReadResult();

        try
        {
            using var client = new ImapClient();

            await EmailServerConnection.ConnectAsync(
                client,
                server,
                EmailServerConnection.ResolvePort(server, EmailServerConnection.ImapPort, EmailServerConnection.ImapImplicitTlsPort),
                _secretProtector,
                cancellationToken);

            var folder = await client.GetFolderAsync(folderName, cancellationToken);
            await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

            var uids = await FindNewMailAsync(folder, state, mailbox, cancellationToken);

            result.HasMore = uids.Count > MaxMessagesPerRead;

            var destination = mailbox.AfterProcessing == EmailMailboxAfterProcessing.MoveToFolder && !string.IsNullOrWhiteSpace(mailbox.ProcessedFolder)
                ? await client.GetFolderAsync(mailbox.ProcessedFolder.Trim(), cancellationToken)
                : null;

            foreach (var uid in uids.Take(MaxMessagesPerRead))
            {
                if (!await ReceiveAsync(folder, uid, address, result, cancellationToken))
                {
                    // The receiver could not take the email right now; the rest wait for the next pass, in order.
                    break;
                }

                await folder.AddFlagsAsync(uid, MessageFlags.Seen, silent: true, cancellationToken);

                if (destination is not null)
                {
                    await folder.MoveToAsync(uid, destination, cancellationToken);
                }

                // Only ever forward: the unread mail of a first read's look-back lies below the position it started at.
                state.LastUid = Math.Max(state.LastUid, uid.Id);
                result.Received++;
            }

            await client.DisconnectAsync(quit: true, cancellationToken);

            state.LastSucceededUtc = _clock.UtcNow;
            state.ConsecutiveFailures = 0;
            state.LastError = null;
            result.Succeeded = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            state.LastFailedUtc = _clock.UtcNow;
            state.ConsecutiveFailures++;
            state.LastError = ex is AuthenticationException
                ? "The mail server refused the sign-in. Check the user name and password (or app password) on the address."
                : ex.Message;

            result.Error = state.LastError;

            _logger.LogWarning(
                ex,
                "Reading the mailbox of email address '{AddressId}' failed ({ConsecutiveFailures} failure(s) in a row).",
                address.ItemId.SanitizeLogValue(),
                state.ConsecutiveFailures);
        }

        if (existing is null)
        {
            await _syncStates.CreateAsync(state, cancellationToken);
        }
        else
        {
            await _syncStates.UpdateAsync(state, cancellationToken);
        }

        if (result.Received > 0 && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Received {Count} email(s) from the mailbox of address '{AddressId}'{More}.",
                result.Received,
                address.ItemId.SanitizeLogValue(),
                result.HasMore ? "; more are waiting" : string.Empty);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<string> TestAsync(EmailMailboxSettings mailbox, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mailbox);

        var server = mailbox.Server ?? new EmailServerSettings();

        if (!server.IsConfigured)
        {
            return "No IMAP server is set.";
        }

        try
        {
            using var client = new ImapClient();

            await EmailServerConnection.ConnectAsync(
                client,
                server,
                EmailServerConnection.ResolvePort(server, EmailServerConnection.ImapPort, EmailServerConnection.ImapImplicitTlsPort),
                _secretProtector,
                cancellationToken);

            var folder = await client.GetFolderAsync(string.IsNullOrWhiteSpace(mailbox.Folder) ? EmailMailboxSettings.DefaultFolder : mailbox.Folder.Trim(), cancellationToken);
            await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

            if (mailbox.AfterProcessing == EmailMailboxAfterProcessing.MoveToFolder && !string.IsNullOrWhiteSpace(mailbox.ProcessedFolder))
            {
                await client.GetFolderAsync(mailbox.ProcessedFolder.Trim(), cancellationToken);
            }

            await client.DisconnectAsync(quit: true, cancellationToken);

            return null;
        }
        catch (AuthenticationException)
        {
            return "The mail server refused the sign-in. Check the user name and password (or app password).";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ex.Message;
        }
    }

    // The mail to receive, oldest first. The first read of a mailbox, or of a renumbered folder, receives only what
    // arrives from now on (plus, when asked, the unread mail of the last few days), so connecting a busy mailbox does
    // not flood the workspace with its history.
    private async Task<IList<UniqueId>> FindNewMailAsync(IMailFolder folder, EmailMailboxSyncState state, EmailMailboxSettings mailbox, CancellationToken cancellationToken)
    {
        if (state.UidValidity != folder.UidValidity || state.LastUid == 0)
        {
            var isRenumbered = state.UidValidity != 0 && state.UidValidity != folder.UidValidity;

            state.UidValidity = folder.UidValidity;
            state.LastUid = folder.UidNext is { } next && next.Id > 0 ? next.Id - 1 : 0;

            if (isRenumbered || mailbox.InitialLookbackDays <= 0)
            {
                return [];
            }

            var since = _clock.UtcNow.AddDays(-Math.Min(mailbox.InitialLookbackDays, 30)).Date;
            var unread = await folder.SearchAsync(SearchQuery.NotSeen.And(SearchQuery.DeliveredAfter(since)), cancellationToken);

            return unread.OrderBy(uid => uid.Id).ToList();
        }

        var range = new UniqueIdRange(new UniqueId(state.UidValidity, state.LastUid + 1), UniqueId.MaxValue);
        var found = await folder.SearchAsync(SearchQuery.Uids(range), cancellationToken);

        // A range ending in * always includes the newest message, even when its UID is below the start, so the result is
        // filtered to what is genuinely new.
        return found
            .Where(uid => uid.Id > state.LastUid)
            .OrderBy(uid => uid.Id)
            .ToList();
    }

    private async Task<bool> ReceiveAsync(IMailFolder folder, UniqueId uid, OmnichannelChannelEndpoint address, EmailMailboxReadResult readResult, CancellationToken cancellationToken)
    {
        InboundEmail email;

        try
        {
            var message = await folder.GetMessageAsync(uid, cancellationToken);

            email = MimeInboundEmailParser.Parse(message);
        }
        catch (FormatException ex)
        {
            // A message that cannot be parsed never will be; it is passed over rather than holding up the mailbox.
            _logger.LogWarning(ex, "A message in the mailbox of email address '{AddressId}' could not be parsed and was passed over.", address.ItemId.SanitizeLogValue());

            return true;
        }

        // The mailbox belongs to the address, so mail in it was sent to the address even when it arrived through an
        // alias, a forward or a Bcc that does not name it.
        var own = OmnichannelEmailAddress.Normalize(address.Value);

        if (own is not null && !email.DeliveredTo.Contains(own, StringComparer.Ordinal))
        {
            email.DeliveredTo.Add(own);
        }

        // The email is committed to the inbox now and processed after the read, so one conversation's AI reply does not
        // hold up the rest of the mailbox.
        var result = await _receiver.ReceiveAsync(email, EmailChannelConstants.Providers.Imap, dispatch: false, cancellationToken);

        if (!string.IsNullOrEmpty(result.InboxMessageId))
        {
            readResult.InboxMessageIds.Add(result.InboxMessageId);
        }

        return result.Status != EmailInboundStatus.Busy;
    }
}
