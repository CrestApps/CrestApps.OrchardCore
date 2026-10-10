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
    /// Finds an address's sending state.
    /// </summary>
    /// <param name="addressId">The address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The state, or <see langword="null"/> when nothing has happened to the address yet.</returns>
    Task<EmailSendingState> FindAsync(string addressId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an address's sending state.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SaveAsync(EmailSendingState state, CancellationToken cancellationToken = default);
}

/// <summary>
/// The default <see cref="IEmailSendingStateStore"/>. A state read once is kept for the scope, so a batch of sends
/// from one address reads it once and every change lands on the same record.
/// </summary>
public sealed class EmailSendingStateStore : IEmailSendingStateStore
{
    private const string Collection = EmailChannelConstants.DeliverabilityCollectionName;

    private readonly ISession _session;
    private readonly Dictionary<string, EmailSendingState> _states = new(StringComparer.Ordinal);

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

        if (_states.TryGetValue(addressId, out var cached))
        {
            return cached;
        }

        var state = await _session.Query<EmailSendingState, EmailSendingStateIndex>(index => index.AddressId == addressId, collection: Collection)
            .FirstOrDefaultAsync(cancellationToken);

        if (state is not null)
        {
            _states[addressId] = state;
        }

        return state;
    }

    public async Task SaveAsync(EmailSendingState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        _states[state.AddressId] = state;

        await _session.SaveAsync(state, collection: Collection, cancellationToken: cancellationToken);
    }
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
