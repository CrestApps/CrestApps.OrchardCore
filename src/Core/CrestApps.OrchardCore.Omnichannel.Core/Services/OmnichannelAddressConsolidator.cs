using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Brings addresses saved with a single channel forward to a type and capabilities, and merges the records that list
/// the same address once per channel into one address with every capability they had.
/// </summary>
public static class OmnichannelAddressConsolidator
{
    /// <summary>
    /// Consolidates the records of an address list in place.
    /// </summary>
    /// <param name="records">The address records, keyed by identifier. Records merged away are removed.</param>
    /// <param name="canonicalize">Brings a value into the form an address of a type is stored in.</param>
    /// <returns>The identifier each merged-away record now resolves to.</returns>
    public static IReadOnlyDictionary<string, string> Consolidate(
        IDictionary<string, OmnichannelChannelEndpoint> records,
        Func<string, string, string> canonicalize)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(canonicalize);

        var retired = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var record in records.Values)
        {
            record.AddressType = record.GetAddressType();
            record.Capabilities = [.. record.GetCapabilities()];
            record.MergedItemIds ??= [];

            if (!string.IsNullOrWhiteSpace(record.Value) && !string.IsNullOrEmpty(record.AddressType))
            {
                record.Value = canonicalize(record.AddressType, record.Value.Trim()) ?? record.Value.Trim();
            }
        }

        var groups = records.Values
            .Where(record => !string.IsNullOrWhiteSpace(record.Value) && !string.IsNullOrEmpty(record.AddressType))
            .GroupBy(record => (Type: record.AddressType.ToUpperInvariant(), Value: record.Value.ToUpperInvariant()))
            .Where(group => group.Count() > 1)
            .ToList();

        foreach (var group in groups)
        {
            // The oldest record survives, so the address keeps the id most of its history already carries.
            var ordered = group
                .OrderBy(record => record.CreatedUtc)
                .ThenBy(record => record.ItemId, StringComparer.Ordinal)
                .ToList();

            var survivor = ordered[0];

            foreach (var other in ordered.Skip(1))
            {
                Absorb(survivor, other);
                records.Remove(other.ItemId);
                retired[other.ItemId] = survivor.ItemId;

                foreach (var alias in other.MergedItemIds ?? [])
                {
                    retired[alias] = survivor.ItemId;
                }
            }
        }

        return retired;
    }

    /// <summary>
    /// Merges a record that lists the same address into the surviving one: its capabilities and settings join the
    /// survivor's, and its identifier becomes one the survivor is known by.
    /// </summary>
    /// <param name="survivor">The address that stays.</param>
    /// <param name="other">The record merged into it.</param>
    public static void Absorb(OmnichannelChannelEndpoint survivor, OmnichannelChannelEndpoint other)
    {
        ArgumentNullException.ThrowIfNull(survivor);
        ArgumentNullException.ThrowIfNull(other);

        survivor.Capabilities ??= [];
        survivor.MergedItemIds ??= [];

        survivor.Capabilities = survivor.Capabilities
            .Concat(other.Capabilities ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        survivor.MergedItemIds = survivor.MergedItemIds
            .Append(other.ItemId)
            .Concat(other.MergedItemIds ?? [])
            .Where(id => !string.IsNullOrEmpty(id) && !string.Equals(id, survivor.ItemId, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // The provider is the texting provider, which only a texting record carried.
        if (string.IsNullOrWhiteSpace(survivor.ProviderName))
        {
            survivor.ProviderName = other.ProviderName;
        }

        if (string.IsNullOrWhiteSpace(survivor.Description))
        {
            survivor.Description = other.Description;
        }

        // Each channel's record kept its own settings (the texting routing on one, the dial-out agents on the other), so
        // they combine without clashing; where both have one, the surviving record's stays.
        if (other.Properties is { Count: > 0 })
        {
            survivor.Properties ??= new Dictionary<string, object>();

            foreach (var (key, value) in other.Properties)
            {
                survivor.Properties.TryAdd(key, value);
            }
        }

        if (other.ModifiedUtc > survivor.ModifiedUtc)
        {
            survivor.ModifiedUtc = other.ModifiedUtc;
        }
    }
}
