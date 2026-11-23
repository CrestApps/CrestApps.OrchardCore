using CrestApps.Core;
using CrestApps.OrchardCore.Consent.Core.Indexes;
using CrestApps.OrchardCore.Consent.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.Consent.Core.Services;

/// <summary>
/// The YesSql <see cref="IConsentAddressOptOutStore"/>. Two opt-outs recorded at the same moment can each add a record;
/// a lookup reads any of them, and a delete removes them all.
/// </summary>
public sealed class ConsentAddressOptOutStore : IConsentAddressOptOutStore
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentAddressOptOutStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ConsentAddressOptOutStore(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(string channel, string address, string purpose, CancellationToken cancellationToken = default)
    {
        var normalized = ConsentAddresses.Normalize(channel, address);
        var normalizedPurpose = ConsentAddresses.NormalizePurpose(purpose);

        if (string.IsNullOrEmpty(channel) || normalized is null || normalizedPurpose is null)
        {
            return false;
        }

        return await _session
            .QueryIndex<ConsentAddressOptOutIndex>(index =>
                index.Channel == channel &&
                index.Address == normalized &&
                index.Purpose == normalizedPurpose)
            .CountAsync(cancellationToken) > 0;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ConsentAddressOptOut>> ListAsync(string channel, string address, CancellationToken cancellationToken = default)
    {
        var normalized = ConsentAddresses.Normalize(channel, address);

        if (string.IsNullOrEmpty(channel) || normalized is null)
        {
            return [];
        }

        var records = await _session
            .Query<ConsentAddressOptOut, ConsentAddressOptOutIndex>(index =>
                index.Channel == channel &&
                index.Address == normalized)
            .ListAsync(cancellationToken);

        return records.ToArray();
    }

    /// <inheritdoc/>
    public async Task<ConsentAddressOptOut> SaveAsync(ConsentAddressOptOut optOut, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(optOut);

        var normalized = ConsentAddresses.Normalize(optOut.Channel, optOut.Address);
        var purpose = ConsentAddresses.NormalizePurpose(optOut.Purpose);

        if (string.IsNullOrEmpty(optOut.Channel) || normalized is null || purpose is null)
        {
            return null;
        }

        var existing = await FindAsync(optOut.Channel, normalized, purpose, cancellationToken);

        if (existing.Count > 0)
        {
            var record = existing[0];

            record.Source = optOut.Source;
            record.OptedOutUtc = optOut.OptedOutUtc;

            await _session.SaveAsync(record, cancellationToken: cancellationToken);

            return record;
        }

        optOut.Address = normalized;
        optOut.Purpose = purpose;

        if (string.IsNullOrEmpty(optOut.ItemId))
        {
            optOut.ItemId = UniqueId.GenerateId();
        }

        await _session.SaveAsync(optOut, cancellationToken: cancellationToken);

        return optOut;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteAsync(string channel, string address, string purpose, CancellationToken cancellationToken = default)
    {
        var normalized = ConsentAddresses.Normalize(channel, address);
        var normalizedPurpose = ConsentAddresses.NormalizePurpose(purpose);

        if (string.IsNullOrEmpty(channel) || normalized is null || normalizedPurpose is null)
        {
            return 0;
        }

        var records = await FindAsync(channel, normalized, normalizedPurpose, cancellationToken);

        foreach (var record in records)
        {
            _session.Delete(record);
        }

        return records.Count;
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<ConsentAddressOptOut> Records, long NextId)> PageAsync(long afterId, int take, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        var records = (await _session
            .Query<ConsentAddressOptOut, ConsentAddressOptOutIndex>(index => index.DocumentId > afterId)
            .OrderBy(index => index.DocumentId)
            .Take(take)
            .ListAsync(cancellationToken))
            .ToArray();

        var nextId = records.Length == take
            ? records.Last().Id
            : 0;

        return (records, nextId);
    }

    private async Task<IReadOnlyList<ConsentAddressOptOut>> FindAsync(string channel, string normalizedAddress, string purpose, CancellationToken cancellationToken)
    {
        var records = await _session
            .Query<ConsentAddressOptOut, ConsentAddressOptOutIndex>(index =>
                index.Channel == channel &&
                index.Address == normalizedAddress &&
                index.Purpose == purpose)
            .ListAsync(cancellationToken);

        return records.ToArray();
    }
}
