using System.Net.Sockets;
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
/// receiving everything twice, and a redelivered email is absorbed by the inbox's <c>Message-ID</c> check. An email that
/// is too large, or that cannot be read or received, is passed over and left unread in the mailbox, so one bad email
/// never holds up the rest.
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

            await ReceiveBatchAsync(folder, uids.Take(MaxMessagesPerRead).ToList(), destination, state, address, result, cancellationToken);

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

            // Shown on the address, so it never carries the exception's own words, which would tell whether a host and
            // port answer on the site's network; those are in the log below.
            state.LastError = ex is AuthenticationException
                ? "The mail server refused the sign-in. Check the user name and password (or app password) on the address."
                : DescribeFailure(ex);

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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The person sees only whether the sign-in was refused or a folder is missing; what went wrong exactly is
            // for the log, so the test cannot be used to find out which hosts and ports answer on the site's network.
            _logger.LogWarning(ex, "Testing the IMAP server '{Host}' failed.", server.Host.SanitizeLogValue());

            return DescribeFailure(ex);
        }
    }

    /// <summary>
    /// Receives one read's mail, in order, and moves the read position past each email it is done with.
    /// </summary>
    /// <remarks>
    /// An email above <see cref="EmailChannelConstants.MaxInboundEmailBytes"/> is never downloaded, and one that cannot
    /// be read or received is passed over: both are left unread where they are, for a person to open in their mail
    /// client, and the position moves past them so the mailbox does not try them again on every read. A failure of the
    /// connection itself ends the read instead, since it is no fault of the email and would fail every email after it;
    /// the next read starts again from the same email.
    /// </remarks>
    /// <param name="folder">The open folder.</param>
    /// <param name="batch">The UIDs to receive, oldest first.</param>
    /// <param name="destination">The folder received mail is moved to, or <see langword="null"/> to leave it in place.</param>
    /// <param name="state">The read position, moved forward as the mail is received.</param>
    /// <param name="address">The address whose mailbox is read.</param>
    /// <param name="result">The result the received emails are counted in.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes once the batch is received or a busy email stops it.</returns>
    internal async Task ReceiveBatchAsync(
        IMailFolder folder,
        IList<UniqueId> batch,
        IMailFolder destination,
        EmailMailboxSyncState state,
        OmnichannelChannelEndpoint address,
        EmailMailboxReadResult result,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        var sizes = await GetSizesAsync(folder, batch, cancellationToken);

        foreach (var uid in batch)
        {
            MessageOutcome outcome;

            if (sizes.TryGetValue(uid.Id, out var size) && size > EmailChannelConstants.MaxInboundEmailBytes)
            {
                _logger.LogWarning(
                    "Message {Uid} in the mailbox of email address '{AddressId}' is {Size} bytes, above the {Limit} bytes an inbound email may be, and was passed over. It is left unread in the mailbox.",
                    uid.Id,
                    address.ItemId.SanitizeLogValue(),
                    size,
                    EmailChannelConstants.MaxInboundEmailBytes);

                outcome = MessageOutcome.PassedOver;
            }
            else
            {
                outcome = await ReceiveAsync(folder, uid, address, result, cancellationToken);
            }

            if (outcome == MessageOutcome.Busy)
            {
                // The receiver could not take the email right now; the rest wait for the next pass, in order.
                break;
            }

            if (outcome == MessageOutcome.Received)
            {
                await folder.AddFlagsAsync(uid, MessageFlags.Seen, silent: true, cancellationToken);

                if (destination is not null)
                {
                    await folder.MoveToAsync(uid, destination, cancellationToken);
                }

                result.Received++;
            }

            // Only ever forward: the unread mail of a first read's look-back lies below the position it started at.
            state.LastUid = Math.Max(state.LastUid, uid.Id);
        }
    }

    // The sizes the server reports, asked for the whole batch at once, so an email too large to receive is passed over
    // without being downloaded. An email the server gives no size for is received as usual.
    private static async Task<Dictionary<uint, uint>> GetSizesAsync(IMailFolder folder, IList<UniqueId> batch, CancellationToken cancellationToken)
    {
        var sizes = new Dictionary<uint, uint>();
        var summaries = await folder.FetchAsync(batch, new FetchRequest(MessageSummaryItems.UniqueId | MessageSummaryItems.Size), cancellationToken);

        if (summaries is null)
        {
            return sizes;
        }

        foreach (var summary in summaries)
        {
            if (summary?.Size is { } size)
            {
                sizes[summary.UniqueId.Id] = size;
            }
        }

        return sizes;
    }

    private static string DescribeFailure(Exception exception)
        => exception is FolderNotFoundException notFound
            ? $"The mailbox has no folder named '{notFound.FolderName}'."
            : EmailServerConnection.DescribeFailure(exception);

    // A failure of the connection (it dropped, or the server stopped answering as it should) says nothing about the
    // email being received and would fail every email after it too, so it ends the read rather than passing the email
    // over. Anything else (an email that cannot be parsed, one the server refuses to hand over, one the receiver cannot
    // take) is that email's own problem.
    private static bool IsEmailFailure(Exception exception)
        => exception is not (OperationCanceledException
            or IOException
            or SocketException
            or ProtocolException
            or ServiceNotConnectedException
            or ServiceNotAuthenticatedException
            or FolderNotOpenException);

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

    private async Task<MessageOutcome> ReceiveAsync(IMailFolder folder, UniqueId uid, OmnichannelChannelEndpoint address, EmailMailboxReadResult readResult, CancellationToken cancellationToken)
    {
        InboundEmail email;

        try
        {
            var message = await folder.GetMessageAsync(uid, cancellationToken);

            email = MimeInboundEmailParser.Parse(message);
        }
        catch (Exception ex) when (IsEmailFailure(ex))
        {
            // A message that cannot be read or parsed never will be; it is passed over rather than holding up the mailbox.
            _logger.LogError(
                ex,
                "Message {Uid} in the mailbox of email address '{AddressId}' could not be read and was passed over. It is left unread in the mailbox.",
                uid.Id,
                address.ItemId.SanitizeLogValue());

            return MessageOutcome.PassedOver;
        }

        // The mailbox belongs to the address, so mail in it was sent to the address even when it arrived through an
        // alias, a forward or a Bcc that does not name it.
        var own = OmnichannelEmailAddress.Normalize(address.Value);

        if (own is not null && !email.DeliveredTo.Contains(own, StringComparer.Ordinal))
        {
            email.DeliveredTo.Add(own);
        }

        EmailInboundResult result;

        try
        {
            // The email is committed to the inbox now and processed after the read, so one conversation's AI reply does
            // not hold up the rest of the mailbox.
            result = await _receiver.ReceiveAsync(email, EmailChannelConstants.Providers.Imap, dispatch: false, cancellationToken);
        }
        catch (Exception ex) when (IsEmailFailure(ex))
        {
            // Retrying an email the receiver fails on would stop every email behind it for good.
            _logger.LogError(
                ex,
                "Message {Uid} in the mailbox of email address '{AddressId}' could not be received and was passed over. It is left unread in the mailbox.",
                uid.Id,
                address.ItemId.SanitizeLogValue());

            return MessageOutcome.PassedOver;
        }

        if (!string.IsNullOrEmpty(result.InboxMessageId))
        {
            readResult.InboxMessageIds.Add(result.InboxMessageId);
        }

        return result.Status == EmailInboundStatus.Busy
            ? MessageOutcome.Busy
            : MessageOutcome.Received;
    }

    // What became of one email of a read.
    private enum MessageOutcome
    {
        // Handed to the receiver; it is marked read (and moved) and counted.
        Received,

        // Too large, unreadable or refused by the receiver; it is left as it is and the read moves past it.
        PassedOver,

        // The receiver could not take it right now; the read stops here and the next one starts again from it.
        Busy,
    }
}
