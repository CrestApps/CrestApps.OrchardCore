namespace CrestApps.OrchardCore.Consent.Core;

/// <summary>
/// The default <see cref="IConsentService"/>: asks the registered <see cref="IConsentSource"/>s in registration order
/// and stops at the first that says the person opted out, and records each change with every source. A source that
/// can't tell (<see cref="ConsentAnswer.Unknown"/>) is named in <see cref="ConsentStatus.UnknownSources"/> and the
/// question goes on to the next source.
/// </summary>
public sealed class DefaultConsentService : IConsentService
{
    private readonly IEnumerable<IConsentSource> _sources;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultConsentService"/> class.
    /// </summary>
    /// <param name="sources">The consent sources.</param>
    public DefaultConsentService(IEnumerable<IConsentSource> sources)
    {
        _sources = sources;
    }

    /// <inheritdoc />
    public Task<ConsentStatus> GetAsync(string channel, string address, string contactId = null, CancellationToken cancellationToken = default)
        => GetAsync(new ConsentQuery
        {
            Channel = channel,
            Address = address,
            ContactKind = string.IsNullOrEmpty(contactId) ? null : ConsentContactKinds.Contact,
            ContactId = contactId,
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<ConsentStatus> GetAsync(ConsentQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrEmpty(query.Channel))
        {
            return ConsentStatus.NotOptedOut;
        }

        List<string> unknown = null;

        foreach (var source in _sources)
        {
            if (query.ExcludedSources?.Contains(source.Name, StringComparer.Ordinal) == true)
            {
                continue;
            }

            switch (await source.GetAnswerAsync(query, cancellationToken))
            {
                case ConsentAnswer.OptedOut:
                    // An opt-out is enough on its own, whatever another source couldn't tell.
                    return ConsentStatus.OptedOutBy(source.Name);
                case ConsentAnswer.Unknown:
                    // A source that can't tell doesn't stop the question: the others still answer, and the caller
                    // decides what the unknown means for its send.
                    (unknown ??= []).Add(source.Name);
                    break;
            }
        }

        return unknown is null ? ConsentStatus.NotOptedOut : ConsentStatus.UnknownTo(unknown);
    }

    /// <inheritdoc />
    public async Task RecordAsync(string channel, string address, ConsentChange change, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(channel);
        ArgumentNullException.ThrowIfNull(change);

        foreach (var source in _sources)
        {
            await source.RecordAsync(channel, address, change, cancellationToken);
        }
    }
}
