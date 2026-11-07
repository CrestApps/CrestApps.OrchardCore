namespace CrestApps.OrchardCore.Consent;

/// <summary>
/// Whether a person may be contacted on a channel at an address, and the place every opt-out and opt-in is recorded:
/// one API over every <see cref="IConsentSource"/>. Every send, dial and campaign asks it; every STOP keyword,
/// unsubscribe link and refusal records through it. Consent to record a call is a different concept and is not kept
/// here.
/// </summary>
public interface IConsentService
{
    /// <summary>
    /// Returns whether a person opted out of <paramref name="channel"/> at <paramref name="address"/>.
    /// </summary>
    /// <param name="channel">The channel, one of <see cref="ConsentChannels"/>.</param>
    /// <param name="address">The address on the channel, or <see langword="null"/>.</param>
    /// <param name="contactId">The CRM contact the person is, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<ConsentStatus> GetAsync(string channel, string address, string contactId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether a person opted out, asking every source the query doesn't exclude until one says so.
    /// </summary>
    /// <param name="query">The question.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<ConsentStatus> GetAsync(ConsentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a person opting out of, or back into, <paramref name="channel"/> at <paramref name="address"/>, with
    /// every source that keeps that kind of record.
    /// </summary>
    /// <param name="channel">The channel, one of <see cref="ConsentChannels"/>.</param>
    /// <param name="address">The address on the channel.</param>
    /// <param name="change">The change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RecordAsync(string channel, string address, ConsentChange change, CancellationToken cancellationToken = default);
}
