namespace CrestApps.OrchardCore.Consent;

/// <summary>
/// A place consent is kept, such as the CRM's do-not-contact flags, a do-not-call list or a customer's consent to
/// marketing. <see cref="IConsentService"/> asks every source and records with every source; each answers only for the
/// channels and the record kinds it keeps.
/// </summary>
public interface IConsentSource
{
    /// <summary>
    /// Gets the source's name, such as one of <see cref="ConsentSourceNames"/>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Returns whether the source says the person opted out; <see langword="false"/> for a channel or a record kind it
    /// doesn't keep.
    /// </summary>
    /// <param name="query">The question.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<bool> HasOptedOutAsync(ConsentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the change, when the source keeps that channel and record kind; does nothing otherwise.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="address">The address on the channel.</param>
    /// <param name="change">The change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RecordAsync(string channel, string address, ConsentChange change, CancellationToken cancellationToken = default);
}
