using CrestApps.OrchardCore.Consent.Core;
using CrestApps.OrchardCore.Consent.Core.Models;
using CrestApps.OrchardCore.Consent.Core.Services;

namespace CrestApps.OrchardCore.Tests.Consent;

/// <summary>
/// An in-memory <see cref="IConsentAddressOptOutStore"/> that normalizes addresses the way the YesSql store does.
/// </summary>
internal sealed class InMemoryConsentAddressOptOutStore : IConsentAddressOptOutStore
{
    /// <summary>
    /// Gets the records kept.
    /// </summary>
    public List<ConsentAddressOptOut> Records { get; } = [];

    public Task<bool> ExistsAsync(string channel, string address, string purpose, CancellationToken cancellationToken = default)
        => Task.FromResult(Find(channel, address, purpose).Any());

    public Task<IReadOnlyList<ConsentAddressOptOut>> ListAsync(string channel, string address, CancellationToken cancellationToken = default)
    {
        var normalized = ConsentAddresses.Normalize(channel, address);

        return Task.FromResult<IReadOnlyList<ConsentAddressOptOut>>(Records
            .Where(record => record.Channel == channel && normalized is not null && record.Address == normalized)
            .ToArray());
    }

    public Task<ConsentAddressOptOut> SaveAsync(ConsentAddressOptOut optOut, CancellationToken cancellationToken = default)
    {
        var existing = Find(optOut.Channel, optOut.Address, optOut.Purpose).FirstOrDefault();

        if (existing is not null)
        {
            existing.Source = optOut.Source;
            existing.OptedOutUtc = optOut.OptedOutUtc;

            return Task.FromResult(existing);
        }

        optOut.Address = ConsentAddresses.Normalize(optOut.Channel, optOut.Address);
        optOut.Purpose = ConsentAddresses.NormalizePurpose(optOut.Purpose);
        optOut.ItemId ??= Guid.NewGuid().ToString("N");
        optOut.Id = Records.Count + 1;
        Records.Add(optOut);

        return Task.FromResult(optOut);
    }

    public Task<int> DeleteAsync(string channel, string address, string purpose, CancellationToken cancellationToken = default)
    {
        var matches = Find(channel, address, purpose).ToArray();

        foreach (var match in matches)
        {
            Records.Remove(match);
        }

        return Task.FromResult(matches.Length);
    }

    public Task<(IReadOnlyList<ConsentAddressOptOut> Records, long NextId)> PageAsync(long afterId, int take, CancellationToken cancellationToken = default)
    {
        var page = Records.Where(record => record.Id > afterId).OrderBy(record => record.Id).Take(take).ToArray();

        return Task.FromResult<(IReadOnlyList<ConsentAddressOptOut>, long)>((page, page.Length == take ? page.Last().Id : 0));
    }

    private IEnumerable<ConsentAddressOptOut> Find(string channel, string address, string purpose)
    {
        var normalized = ConsentAddresses.Normalize(channel, address);
        var normalizedPurpose = ConsentAddresses.NormalizePurpose(purpose);

        if (normalized is null || normalizedPurpose is null)
        {
            return [];
        }

        return Records.Where(record =>
            record.Channel == channel &&
            record.Address == normalized &&
            record.Purpose == normalizedPurpose);
    }
}
