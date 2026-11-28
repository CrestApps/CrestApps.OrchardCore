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
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Mailbox;

/// <summary>
/// Reads an address's mailbox over IMAP. Each read takes the mail whose UID is above the last one received, in order,
/// hands it to the inbound receiver, then marks it read (and moves it, when the address says so). The position is kept per
/// address with the folder's <c>UIDVALIDITY</c>, so a renumbered folder starts again from the newest mail rather than
/// receiving everything twice, and a redelivered email is absorbed by the inbox's <c>Message-ID</c> check. An email that
/// is too large, or that cannot be read, is passed over and left unread in the mailbox, so one bad email never holds up
/// the rest. One the receiver fails on is tried again by the next few reads, so a passing fault loses no email.
/// </summary>
public sealed class ImapEmailMailboxReader : IEmailMailboxReader
{
    /// <summary>
    /// The most emails one read takes, so a backlog is worked through over several passes rather than in one long one.
    /// </summary>
    public const int MaxMessagesPerRead = 50;

    /// <summary>
    /// How many reads try an email the inbound receiver fails on before it is passed over. A failure that passes (the
    /// database or the file store briefly out of reach) is retried rather than losing the email; one that keeps coming
    /// back is the email's own. The read moves past a failing email meanwhile, so it never holds up the mail after it.
    /// </summary>
    public const int MaxReceiveAttempts = 5;

    // The most failing emails remembered for another try. More than this failing at once is a fault of the site, which
    // fails the read itself long before; any beyond it are passed over.
    private const int MaxRetryUids = 200;

    private readonly IEmailInboundReceiver _receiver;
    private readonly ICatalog<EmailMailboxSyncState> _syncStates;
    private readonly IEmailSecretProtector _secretProtector;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImapEmailMailboxReader"/> class.
    /// </summary>
    /// <param name="receiver">The inbound receiver each email is handed to.</param>
    /// <param name="syncStates">The catalog the read positions are kept in.</param>
    /// <param name="secretProtector">The protector the mailbox password is read with.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ImapEmailMailboxReader(
        IEmailInboundReceiver receiver,
        ICatalog<EmailMailboxSyncState> syncStates,
        IEmailSecretProtector secretProtector,
        IClock clock,
        ILogger<ImapEmailMailboxReader> logger,
        IStringLocalizer<ImapEmailMailboxReader> stringLocalizer)
    {
        _receiver = receiver;
        _syncStates = syncStates;
        _secretProtector = secretProtector;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
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
            return new EmailMailboxReadResult { Error = S["The address reads its mailbox, but no IMAP server is set on the address."] };
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
            state.RetryUids = [];
        }

        state.RetryUids ??= [];

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

            await ReceiveBatchAsync(folder, uids.Take(MaxMessagesPerRead).ToList(), destination, state, address, mailbox.TrustedAuthenticationServerId, result, cancellationToken);

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
                ? S["The mail server refused the sign-in. Check the user name and password (or app password) on the address."]
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
            return S["No IMAP server is set."];
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
    /// be read is passed over: both are left unread where they are, for a person to open in their mail client, and the
    /// position moves past them so the mailbox does not try them again on every read. One the receiver fails on is
    /// remembered and tried again by the next reads, up to <see cref="MaxReceiveAttempts"/> times. A failure of the
    /// connection itself ends the read instead, since it is no fault of the email and would fail every email after it;
    /// the next read starts again from the same email.
    /// </remarks>
    /// <param name="folder">The open folder.</param>
    /// <param name="batch">The UIDs to receive, oldest first.</param>
    /// <param name="destination">The folder received mail is moved to, or <see langword="null"/> to leave it in place.</param>
    /// <param name="state">The read position, moved forward as the mail is received.</param>
    /// <param name="address">The address whose mailbox is read.</param>
    /// <param name="trustedAuthenticationServerId">The <c>authserv-id</c> whose <c>Authentication-Results</c> are
    /// believed, or <see langword="null"/> to read the topmost header.</param>
    /// <param name="result">The result the received emails are counted in.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes once the batch is received or a busy email stops it.</returns>
    internal async Task ReceiveBatchAsync(
        IMailFolder folder,
        IList<UniqueId> batch,
        IMailFolder destination,
        EmailMailboxSyncState state,
        OmnichannelChannelEndpoint address,
        string trustedAuthenticationServerId,
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
                    "Message {Uid} in the mailbox of email address '{AddressId}' is {Size} bytes, above the size an inbound email may be, and was passed over. It is left unread in the mailbox.",
                    uid.Id,
                    address.ItemId.SanitizeLogValue(),
                    size);

                outcome = MessageOutcome.PassedOver;
            }
            else
            {
                outcome = await ReceiveAsync(folder, uid, address, trustedAuthenticationServerId, result, cancellationToken);
            }

            if (outcome == MessageOutcome.Busy)
            {
                // The receiver could not take the email right now; the rest wait for the next pass, in order.
                break;
            }

            if (outcome == MessageOutcome.Failed)
            {
                RememberFailure(state, uid, address);
            }
            else
            {
                state.RetryUids.Remove(uid.Id);
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

    // Keeps an email the receiver failed on for the next read to try again, until it has failed on every try it gets.
    private void RememberFailure(EmailMailboxSyncState state, UniqueId uid, OmnichannelChannelEndpoint address)
    {
        var attempts = (state.RetryUids.TryGetValue(uid.Id, out var previous) ? previous : 0) + 1;

        if (attempts < MaxReceiveAttempts && (state.RetryUids.ContainsKey(uid.Id) || state.RetryUids.Count < MaxRetryUids))
        {
            state.RetryUids[uid.Id] = attempts;

            _logger.LogWarning(
                "Message {Uid} in the mailbox of email address '{AddressId}' could not be received ({Attempts} of {MaxAttempts} tries); the next read tries it again.",
                uid.Id,
                address.ItemId.SanitizeLogValue(),
                attempts,
                MaxReceiveAttempts);

            return;
        }

        state.RetryUids.Remove(uid.Id);

        _logger.LogError(
            "Message {Uid} in the mailbox of email address '{AddressId}' could not be received after {Attempts} tries and was passed over. It is left unread in the mailbox.",
            uid.Id,
            address.ItemId.SanitizeLogValue(),
            attempts);
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

    private string DescribeFailure(Exception exception)
        => exception is FolderNotFoundException notFound
            ? S["The mailbox has no folder named '{0}'.", notFound.FolderName]
            : EmailServerConnection.DescribeFailure(exception, S);

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
    internal async Task<IList<UniqueId>> FindNewMailAsync(IMailFolder folder, EmailMailboxSyncState state, EmailMailboxSettings mailbox, CancellationToken cancellationToken)
    {
        if (state.UidValidity != folder.UidValidity || state.LastUid == 0)
        {
            var isRenumbered = state.UidValidity != 0 && state.UidValidity != folder.UidValidity;

            state.UidValidity = folder.UidValidity;
            state.LastUid = folder.UidNext is { } next && next.Id > 0 ? next.Id - 1 : 0;

            // The UIDs of a renumbered folder name other emails now.
            state.RetryUids = [];

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
        var newMail = found
            .Where(uid => uid.Id > state.LastUid)
            .OrderBy(uid => uid.Id)
            .ToList();

        // The emails the receiver failed on come first, while they are still in the folder. One a person moved or
        // deleted meanwhile is no longer there, and is forgotten.
        var retryUids = await FindRetryMailAsync(folder, state, cancellationToken);

        return [.. retryUids, .. newMail.Where(uid => !state.RetryUids.ContainsKey(uid.Id))];
    }

    private static async Task<IList<UniqueId>> FindRetryMailAsync(IMailFolder folder, EmailMailboxSyncState state, CancellationToken cancellationToken)
    {
        if (state.RetryUids.Count == 0)
        {
            return [];
        }

        var wanted = new UniqueIdSet(state.RetryUids.Keys.Order().Select(id => new UniqueId(state.UidValidity, id)), SortOrder.Ascending);
        var present = (await folder.SearchAsync(SearchQuery.Uids(wanted), cancellationToken))
            .Where(uid => state.RetryUids.ContainsKey(uid.Id))
            .OrderBy(uid => uid.Id)
            .ToList();

        foreach (var gone in state.RetryUids.Keys.Except(present.Select(uid => uid.Id)).ToArray())
        {
            state.RetryUids.Remove(gone);
        }

        return present;
    }

    private async Task<MessageOutcome> ReceiveAsync(
        IMailFolder folder,
        UniqueId uid,
        OmnichannelChannelEndpoint address,
        string trustedAuthenticationServerId,
        EmailMailboxReadResult readResult,
        CancellationToken cancellationToken)
    {
        InboundEmail email;
        string authenticationResults;

        try
        {
            var message = await folder.GetMessageAsync(uid, cancellationToken);

            email = MimeInboundEmailParser.Parse(message);

            // Every Authentication-Results header, topmost first: the one believed is the trusted server's own when the
            // mailbox names one, otherwise the topmost.
            authenticationResults = InboundEmailAuthentication.SelectAuthenticationResults(
                message.Headers
                    .Where(header => string.Equals(header.Field, InboundEmailAuthentication.AuthenticationResultsHeader, StringComparison.OrdinalIgnoreCase))
                    .Select(header => header.Value),
                trustedAuthenticationServerId);
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

        // A mailbox host that checks DMARC (Microsoft 365, Google Workspace) adds its own Authentication-Results above any
        // header the sender wrote, so the topmost one says whether the From domain passed. When the mailbox names the
        // host's authserv-id, only a header that host wrote is believed, so a host that adds none never leaves the
        // sender's own header to be read instead.
        email.IsFromDomainAuthenticated = InboundEmailAuthentication.HasDmarcPass(authenticationResults, email.From?.Address);

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
            // The failure may pass (the database or the file store briefly out of reach), so the email is tried again on
            // the next reads rather than lost; the read moves on meanwhile, so it holds up no email behind it.
            _logger.LogWarning(
                ex,
                "The inbound receiver failed on message {Uid} in the mailbox of email address '{AddressId}'.",
                uid.Id,
                address.ItemId.SanitizeLogValue());

            return MessageOutcome.Failed;
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

        // Too large or unreadable; it is left as it is and the read moves past it.
        PassedOver,

        // The receiver failed on it; the read moves past it, and the next read tries it again (up to its limit).
        Failed,

        // The receiver could not take it right now; the read stops here and the next one starts again from it.
        Busy,
    }
}
