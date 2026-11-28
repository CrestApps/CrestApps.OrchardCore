using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;

/// <summary>
/// Stores each email address's sending state.
/// </summary>
public interface IEmailSendingStateStore
{
    /// <summary>
    /// Finds an address's sending state, to decide from. A record this scope already read is returned as it was read,
    /// so it is never changed and saved: a change starts from <see cref="FindFreshAsync"/>.
    /// </summary>
    /// <param name="addressId">The address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The state, or <see langword="null"/> when nothing has happened to the address yet.</returns>
    Task<EmailSendingState> FindAsync(string addressId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads an address's sending state again from the database, to change it: whatever this scope read before is
    /// dropped, so a pause or a turn another process wrote since is built on rather than overwritten. When the address
    /// has more than one record, the oldest is kept, the others are merged into it and deleted.
    /// </summary>
    /// <param name="addressId">The address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The state, or <see langword="null"/> when nothing has happened to the address yet.</returns>
    Task<EmailSendingState> FindFreshAsync(string addressId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an address's sending state: a new one, or one read with <see cref="FindFreshAsync"/>.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SaveAsync(EmailSendingState state, CancellationToken cancellationToken = default);
}

/// <summary>
/// The default <see cref="IEmailSendingStateStore"/>. An address has one record, which the unique index on its address
/// keeps so; every reader takes the oldest record, so the choice is the same everywhere even for an address that got a
/// second record before the index existed.
/// </summary>
public sealed class EmailSendingStateStore : IEmailSendingStateStore
{
    private const string Collection = EmailChannelConstants.DeliverabilityCollectionName;

    private readonly ISession _session;

    public EmailSendingStateStore(ISession session)
    {
        _session = session;
    }

    public async Task<EmailSendingState> FindAsync(string addressId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(addressId))
        {
            return null;
        }

        return await _session.Query<EmailSendingState, EmailSendingStateIndex>(index => index.AddressId == addressId, collection: Collection)
            .OrderBy(index => index.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<EmailSendingState> FindFreshAsync(string addressId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(addressId))
        {
            return null;
        }

        // A detach also drops a change this scope saved but has not written yet, so the scope's pending writes go to the
        // database first; the reads below then see them, inside this scope's transaction.
        await _session.FlushAsync(cancellationToken);

        // The session hands back the copy it already tracks, however the row changed since, so that copy is dropped and
        // the records are read again.
        _session.Detach(await ListAsync(addressId, cancellationToken), Collection);

        var states = await ListAsync(addressId, cancellationToken);

        if (states.Count == 0)
        {
            return null;
        }

        var state = states[0];

        if (states.Count > 1)
        {
            // Two processes created a record each before the address's unique index existed. The oldest is the one
            // every reader takes; what the others hold is kept on it, so no pause or wait is lost with them.
            foreach (var duplicate in states.Skip(1))
            {
                Merge(state, duplicate);
                _session.Delete(duplicate, Collection);
            }

            await _session.SaveAsync(state, collection: Collection, cancellationToken: cancellationToken);
        }

        return state;
    }

    public async Task SaveAsync(EmailSendingState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        await _session.SaveAsync(state, collection: Collection, cancellationToken: cancellationToken);
    }

    private async Task<List<EmailSendingState>> ListAsync(string addressId, CancellationToken cancellationToken)
        => (await _session.Query<EmailSendingState, EmailSendingStateIndex>(index => index.AddressId == addressId, collection: Collection)
            .OrderBy(index => index.Id)
            .ListAsync(cancellationToken)).ToList();

    // Keeps the stricter of the two: the pause that holds longest, the longer runs of throttles and blocks, the later
    // turn and resume, and every receiving domain's wait.
    internal static void Merge(EmailSendingState target, EmailSendingState other)
    {
        if (PauseRank(other) > PauseRank(target) ||
            (PauseRank(other) == PauseRank(target) && other.PausedUntilUtc > target.PausedUntilUtc))
        {
            target.PauseKind = other.PauseKind;
            target.PausedUntilUtc = other.PausedUntilUtc;
            target.PausedUtc = other.PausedUtc;
            target.PauseReason = other.PauseReason;
        }

        target.ConsecutiveThrottles = Math.Max(target.ConsecutiveThrottles, other.ConsecutiveThrottles);
        target.ConsecutiveBlocks = Math.Max(target.ConsecutiveBlocks, other.ConsecutiveBlocks);
        target.NextBulkSlotUtc = Later(target.NextBulkSlotUtc, other.NextBulkSlotUtc);
        target.ResumedUtc = Later(target.ResumedUtc, other.ResumedUtc);
        target.ModifiedUtc = target.ModifiedUtc >= other.ModifiedUtc ? target.ModifiedUtc : other.ModifiedUtc;
        target.DomainBackoffs ??= new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        if (other.DomainBackoffs is null)
        {
            return;
        }

        foreach (var (domain, until) in other.DomainBackoffs)
        {
            if (!target.DomainBackoffs.TryGetValue(domain, out var current) || until > current)
            {
                target.DomainBackoffs[domain] = until;
            }
        }
    }

    // A pause that waits for a person outranks one that ends by itself, which outranks none.
    private static int PauseRank(EmailSendingState state)
        => state.PauseKind switch
        {
            EmailSendingPauseKind.PoorHealth => 2,
            EmailSendingPauseKind.None => 0,
            _ => 1,
        };

    private static DateTime? Later(DateTime? first, DateTime? second)
        => second > first || first is null ? second : first;
}

/// <summary>
/// Finds an outbound email the business sent, by the Message-ID or provider identifier it was sent with.
/// </summary>
public interface IEmailSentMessageFinder
{
    /// <summary>
    /// Finds the email.
    /// </summary>
    /// <param name="messageId">The Message-ID or provider identifier, without angle brackets.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The email, or <see langword="null"/> when none was recorded with that identifier.</returns>
    Task<OmnichannelMessage> FindAsync(string messageId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The default <see cref="IEmailSentMessageFinder"/>, reading the omnichannel message index.
/// </summary>
public sealed class EmailSentMessageFinder : IEmailSentMessageFinder
{
    private readonly ISession _session;

    public EmailSentMessageFinder(ISession session)
    {
        _session = session;
    }

    public async Task<OmnichannelMessage> FindAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(messageId))
        {
            return null;
        }

        return await _session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                index => index.Channel == OmnichannelConstants.Channels.Email && index.ProviderMessageId == messageId && !index.IsInbound,
                collection: OmnichannelConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
