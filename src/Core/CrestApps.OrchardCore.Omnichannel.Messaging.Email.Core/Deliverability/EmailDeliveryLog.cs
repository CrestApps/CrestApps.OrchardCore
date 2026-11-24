using System.Data.Common;
using System.Globalization;
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
    /// Counts the emails to a recipient that had an entry of one kind since a time, across every address. Entries of one
    /// email count once, so a provider reporting each retry of an email counts as that one email; an entry that names no
    /// email counts on its own.
    /// </summary>
    /// <param name="recipient">The recipient, normalized.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="sinceUtc">The start of the window.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count.</returns>
    Task<int> CountMessagesForRecipientAsync(string recipient, EmailDeliveryEventKind kind, DateTime sinceUtc, CancellationToken cancellationToken = default);

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
    /// Deletes entries older than a time, oldest first, at most <paramref name="maxEntries"/> of them (and never more than
    /// 500 at once). The deletes run in the caller's unit of work, which commits them.
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

    // The most entries one prune deletes: each takes one placeholder in a statement, and this many stays under the
    // parameter limit of every database YesSql supports.
    private const int MaxPruneBatch = 500;

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

    public async Task<int> CountMessagesForRecipientAsync(string recipient, EmailDeliveryEventKind kind, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var value = (int)kind;
        var key = EmailDeliveryLogIndexProvider.Truncate(recipient, EmailDeliveryLogIndex.RecipientLength);

        // A recipient has a handful of such entries in a window, so the rows are read and told apart here.
        var rows = (await _session.QueryIndex<EmailDeliveryLogIndex>(index => index.Recipient == key && index.Kind == value && index.OccurredUtc >= sinceUtc, collection: Collection)
            .ListAsync(cancellationToken)).ToArray();

        return rows.Where(row => !string.IsNullOrEmpty(row.MessageId)).Select(row => row.MessageId).Distinct(StringComparer.Ordinal).Count() +
            rows.Count(row => string.IsNullOrEmpty(row.MessageId));
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
        // Only the index rows of the oldest entries are read, through the retention index, and the rows and their
        // documents are deleted by set-based statements. Loading every entry's document and deleting it through the
        // session one by one is what kept the prune behind a busy address's log.
        var rowIds = (await _session.QueryIndex<EmailDeliveryLogIndex>(index => index.OccurredUtc < beforeUtc, collection: Collection)
            .OrderBy(index => index.OccurredUtc)
            .Take(Math.Clamp(maxEntries, 1, MaxPruneBatch))
            .ListAsync(cancellationToken))
            .Select(row => row.Id)
            .ToArray();

        if (rowIds.Length == 0)
        {
            return 0;
        }

        var configuration = _session.Store.Configuration;
        var dialect = configuration.SqlDialect;
        var indexTable = dialect.QuoteForTableName(configuration.TablePrefix + configuration.TableNameConvention.GetIndexTable(typeof(EmailDeliveryLogIndex), Collection), configuration.Schema);
        var documentTable = dialect.QuoteForTableName(configuration.TablePrefix + configuration.TableNameConvention.GetDocumentTable(Collection), configuration.Schema);
        var idColumn = dialect.QuoteForColumnName("Id");
        var documentIdColumn = dialect.QuoteForColumnName("DocumentId");

        // The statements run on the session's own transaction, after its pending writes, so they commit or roll back
        // with the caller's unit of work.
        await _session.FlushAsync(cancellationToken);
        var transaction = await _session.BeginTransactionAsync(cancellationToken);

        var documentIds = new List<long>(rowIds.Length);

        await using (var select = CreateCommand(transaction, $"SELECT {documentIdColumn} FROM {indexTable} WHERE {idColumn} IN ({Placeholders("r", rowIds.Length)})", "r", rowIds))
        {
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                documentIds.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
            }
        }

        // The index rows go first: on SQL Server each one has a foreign key to its document.
        await using (var deleteRows = CreateCommand(transaction, $"DELETE FROM {indexTable} WHERE {idColumn} IN ({Placeholders("r", rowIds.Length)})", "r", rowIds))
        {
            await deleteRows.ExecuteNonQueryAsync(cancellationToken);
        }

        if (documentIds.Count > 0)
        {
            await using var deleteDocuments = CreateCommand(transaction, $"DELETE FROM {documentTable} WHERE {idColumn} IN ({Placeholders("d", documentIds.Count)})", "d", documentIds);

            await deleteDocuments.ExecuteNonQueryAsync(cancellationToken);
        }

        return rowIds.Length;
    }

    // A command on the transaction with one parameter per identifier, so the statement has the same shape on every
    // supported database.
    private static DbCommand CreateCommand(DbTransaction transaction, string sql, string prefix, IReadOnlyList<long> ids)
    {
        var command = transaction.Connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = sql;

        for (var i = 0; i < ids.Count; i++)
        {
            var parameter = command.CreateParameter();

            parameter.ParameterName = "@" + prefix + i.ToString(CultureInfo.InvariantCulture);
            parameter.Value = ids[i];
            command.Parameters.Add(parameter);
        }

        return command;
    }

    private static string Placeholders(string prefix, int count)
        => string.Join(", ", Enumerable.Range(0, count).Select(index => "@" + prefix + index.ToString(CultureInfo.InvariantCulture)));
}
