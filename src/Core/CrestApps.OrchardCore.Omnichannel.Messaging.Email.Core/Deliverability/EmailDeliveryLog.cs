using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;

/// <summary>
/// Records what each email address sends and what happens to it, and answers the counts sending limits and sending
/// health are decided from.
/// </summary>
public interface IEmailDeliveryLog
{
    /// <summary>
    /// Records an entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RecordAsync(EmailDeliveryLogEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether an entry recording the provider event <paramref name="eventId"/> exists.
    /// </summary>
    /// <param name="eventId">The provider event identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the event was recorded before.</returns>
    Task<bool> HasEventAsync(string eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the entries of one kind for an address since a time.
    /// </summary>
    /// <param name="addressId">The address.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="sinceUtc">The start of the window.</param>
    /// <param name="recipientDomain">The receiving domain to count only, or <see langword="null"/> for all.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count.</returns>
    Task<int> CountAsync(string addressId, EmailDeliveryEventKind kind, DateTime sinceUtc, string recipientDomain = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the entries of one kind for a recipient since a time, across every address.
    /// </summary>
    /// <param name="recipient">The recipient, normalized.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="sinceUtc">The start of the window.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count.</returns>
    Task<int> CountForRecipientAsync(string recipient, EmailDeliveryEventKind kind, DateTime sinceUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the time of the <paramref name="position"/>-th oldest sent email (zero-based) of an address since a time:
    /// the email whose leaving the window frees the next send.
    /// </summary>
    /// <param name="addressId">The address.</param>
    /// <param name="sinceUtc">The start of the window.</param>
    /// <param name="position">The zero-based position, oldest first.</param>
    /// <param name="recipientDomain">The receiving domain to look at only, or <see langword="null"/> for all.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The time, or <see langword="null"/> when there is no such email.</returns>
    Task<DateTime?> GetSentTimeAsync(string addressId, DateTime sinceUtc, int position, string recipientDomain = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets when the address last sent bulk mail.
    /// </summary>
    /// <param name="addressId">The address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The time, or <see langword="null"/> when it never did.</returns>
    Task<DateTime?> GetLastBulkSentUtcAsync(string addressId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the address that sent an email, by its message identifier or, failing that, by the newest email sent to
    /// the recipient.
    /// </summary>
    /// <param name="messageId">The message identifier, without angle brackets.</param>
    /// <param name="recipient">The recipient, normalized.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The address identifier, or <see langword="null"/> when none is known.</returns>
    Task<string> FindSendingAddressIdAsync(string messageId, string recipient, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes entries older than a time, at most <paramref name="maxEntries"/> of them.
    /// </summary>
    /// <param name="beforeUtc">The cut-off.</param>
    /// <param name="maxEntries">The most entries to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many were deleted.</returns>
    Task<int> PruneAsync(DateTime beforeUtc, int maxEntries, CancellationToken cancellationToken = default);
}

/// <summary>
/// The default <see cref="IEmailDeliveryLog"/>, stored as documents in the deliverability collection.
/// </summary>
public sealed class EmailDeliveryLog : IEmailDeliveryLog
{
    private const string Collection = EmailChannelConstants.DeliverabilityCollectionName;

    private static readonly int _sent = (int)EmailDeliveryEventKind.Sent;

    private readonly ISession _session;

    public EmailDeliveryLog(ISession session)
    {
        _session = session;
    }

    public async Task RecordAsync(EmailDeliveryLogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        entry.ItemId ??= UniqueId.GenerateId();
        entry.Detail = entry.Detail is { Length: > 500 } ? entry.Detail[..500] : entry.Detail;

        await _session.SaveAsync(entry, collection: Collection, cancellationToken: cancellationToken);
    }

    public async Task<bool> HasEventAsync(string eventId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(eventId))
        {
            return false;
        }

        var id = EmailDeliveryLogIndexProvider.Truncate(eventId, EmailDeliveryLogIndex.EventIdLength);

        return await _session.QueryIndex<EmailDeliveryLogIndex>(index => index.EventId == id, collection: Collection)
            .CountAsync(cancellationToken) > 0;
    }

    public Task<int> CountAsync(string addressId, EmailDeliveryEventKind kind, DateTime sinceUtc, string recipientDomain = null, CancellationToken cancellationToken = default)
    {
        var value = (int)kind;

        return recipientDomain is null
            ? _session.QueryIndex<EmailDeliveryLogIndex>(index => index.AddressId == addressId && index.Kind == value && index.OccurredUtc >= sinceUtc, collection: Collection)
                .CountAsync(cancellationToken)
            : _session.QueryIndex<EmailDeliveryLogIndex>(index => index.AddressId == addressId && index.Kind == value && index.RecipientDomain == recipientDomain && index.OccurredUtc >= sinceUtc, collection: Collection)
                .CountAsync(cancellationToken);
    }

    public Task<int> CountForRecipientAsync(string recipient, EmailDeliveryEventKind kind, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var value = (int)kind;
        var key = EmailDeliveryLogIndexProvider.Truncate(recipient, EmailDeliveryLogIndex.RecipientLength);

        return _session.QueryIndex<EmailDeliveryLogIndex>(index => index.Recipient == key && index.Kind == value && index.OccurredUtc >= sinceUtc, collection: Collection)
            .CountAsync(cancellationToken);
    }

    public async Task<DateTime?> GetSentTimeAsync(string addressId, DateTime sinceUtc, int position, string recipientDomain = null, CancellationToken cancellationToken = default)
    {
        var query = recipientDomain is null
            ? _session.QueryIndex<EmailDeliveryLogIndex>(index => index.AddressId == addressId && index.Kind == _sent && index.OccurredUtc >= sinceUtc, collection: Collection)
            : _session.QueryIndex<EmailDeliveryLogIndex>(index => index.AddressId == addressId && index.Kind == _sent && index.RecipientDomain == recipientDomain && index.OccurredUtc >= sinceUtc, collection: Collection);

        var entry = await query
            .OrderBy(index => index.OccurredUtc)
            .Skip(Math.Max(0, position))
            .Take(1)
            .FirstOrDefaultAsync(cancellationToken);

        return entry?.OccurredUtc;
    }

    public async Task<DateTime?> GetLastBulkSentUtcAsync(string addressId, CancellationToken cancellationToken = default)
    {
        var entry = await _session.QueryIndex<EmailDeliveryLogIndex>(index => index.AddressId == addressId && index.Kind == _sent && index.IsBulk, collection: Collection)
            .OrderByDescending(index => index.OccurredUtc)
            .Take(1)
            .FirstOrDefaultAsync(cancellationToken);

        return entry?.OccurredUtc;
    }

    public async Task<string> FindSendingAddressIdAsync(string messageId, string recipient, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(messageId))
        {
            var id = EmailDeliveryLogIndexProvider.Truncate(messageId, EmailDeliveryLogIndex.MessageIdLength);

            var byMessage = await _session.QueryIndex<EmailDeliveryLogIndex>(index => index.MessageId == id && index.Kind == _sent, collection: Collection)
                .Take(1)
                .FirstOrDefaultAsync(cancellationToken);

            if (byMessage is not null)
            {
                return byMessage.AddressId;
            }
        }

        if (string.IsNullOrEmpty(recipient))
        {
            return null;
        }

        var key = EmailDeliveryLogIndexProvider.Truncate(recipient, EmailDeliveryLogIndex.RecipientLength);

        var byRecipient = await _session.QueryIndex<EmailDeliveryLogIndex>(index => index.Recipient == key && index.Kind == _sent, collection: Collection)
            .OrderByDescending(index => index.OccurredUtc)
            .Take(1)
            .FirstOrDefaultAsync(cancellationToken);

        return byRecipient?.AddressId;
    }

    public async Task<int> PruneAsync(DateTime beforeUtc, int maxEntries, CancellationToken cancellationToken = default)
    {
        var entries = await _session.Query<EmailDeliveryLogEntry, EmailDeliveryLogIndex>(index => index.OccurredUtc < beforeUtc, collection: Collection)
            .OrderBy(index => index.OccurredUtc)
            .Take(Math.Max(1, maxEntries))
            .ListAsync(cancellationToken);

        var count = 0;

        foreach (var entry in entries)
        {
            _session.Delete(entry, Collection);
            count++;
        }

        return count;
    }
}
