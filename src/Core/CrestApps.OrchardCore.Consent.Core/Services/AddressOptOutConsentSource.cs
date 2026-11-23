using CrestApps.OrchardCore.Consent.Core.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Consent.Core.Services;

/// <summary>
/// The opt-outs for one purpose recorded against an address (<see cref="ConsentAddressOptOut"/>), as a consent source.
/// It answers a question for a purpose when the address opted out of that purpose on the channel, whoever the question
/// names: an opt-out at an address holds for every customer or contact sent to it. A question with no purpose asks
/// whether every purpose is blocked, which this source never says. It records only changes with a purpose: an opt-out
/// is saved, an opt-in deletes it. An opt-out of every purpose is the CRM's to keep.
/// </summary>
public sealed class AddressOptOutConsentSource : IConsentSource
{
    private readonly IConsentAddressOptOutStore _store;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddressOptOutConsentSource"/> class.
    /// </summary>
    /// <param name="store">The store of address opt-outs.</param>
    /// <param name="clock">The clock.</param>
    public AddressOptOutConsentSource(IConsentAddressOptOutStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    /// <inheritdoc/>
    public string Name
        => ConsentSourceNames.AddressOptOuts;

    /// <inheritdoc/>
    public Task<bool> HasOptedOutAsync(ConsentQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrEmpty(query.Channel) ||
            string.IsNullOrWhiteSpace(query.Address) ||
            string.IsNullOrWhiteSpace(query.Purpose))
        {
            return Task.FromResult(false);
        }

        return _store.ExistsAsync(query.Channel, query.Address, query.Purpose, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RecordAsync(string channel, string address, ConsentChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (string.IsNullOrEmpty(channel) ||
            string.IsNullOrWhiteSpace(address) ||
            string.IsNullOrWhiteSpace(change.Purpose))
        {
            return;
        }

        if (!change.OptedOut)
        {
            await _store.DeleteAsync(channel, address, change.Purpose, cancellationToken);

            return;
        }

        await _store.SaveAsync(new ConsentAddressOptOut
        {
            Channel = channel,
            Address = address,
            Purpose = change.Purpose,
            Source = change.Source,
            OptedOutUtc = _clock.UtcNow,
        }, cancellationToken);
    }
}
