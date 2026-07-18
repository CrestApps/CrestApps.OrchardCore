using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Process-wide registry that tracks which tenant owns each Asterisk ARI (BaseUrl, ApplicationName)
/// pair on this node. The backing store is a static <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// so ownership is shared across all per-tenant DI containers without requiring <c>IDistributedLock</c>
/// or <c>IDistributedCache</c>, which are tenant-scoped in Orchard Core. Each claim is reference-counted
/// by a per-shell-generation token so a pair is only released after every generation holding it releases,
/// which keeps ownership stable across an Orchard shell reload where a retiring and an activating
/// generation of the same tenant briefly overlap.
/// </summary>
internal sealed class AsteriskAriApplicationOwnershipRegistry : IAsteriskAriApplicationOwnershipRegistry
{
    private static readonly ConcurrentDictionary<string, OwnershipEntry> _ownership = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public bool TryClaim(string baseUrl, string applicationName, string tenantName, string ownershipToken)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) ||
            string.IsNullOrWhiteSpace(applicationName) ||
            string.IsNullOrWhiteSpace(ownershipToken))
        {
            return true;
        }

        var key = NormalizeKey(baseUrl, applicationName);

        while (true)
        {
            if (_ownership.TryGetValue(key, out var existing))
            {
                if (!string.Equals(existing.TenantName, tenantName, StringComparison.Ordinal))
                {
                    return false;
                }

                if (existing.Tokens.Contains(ownershipToken))
                {
                    return true;
                }

                var updated = new OwnershipEntry(existing.TenantName, existing.Tokens.Add(ownershipToken));

                if (_ownership.TryUpdate(key, updated, existing))
                {
                    return true;
                }

                continue;
            }

            var created = new OwnershipEntry(tenantName, ImmutableHashSet.Create(StringComparer.Ordinal, ownershipToken));

            if (_ownership.TryAdd(key, created))
            {
                return true;
            }
        }
    }

    /// <inheritdoc/>
    public void Release(string ownershipToken)
    {
        if (string.IsNullOrWhiteSpace(ownershipToken))
        {
            return;
        }

        foreach (var key in _ownership.Keys)
        {
            while (_ownership.TryGetValue(key, out var existing) && existing.Tokens.Contains(ownershipToken))
            {
                var remaining = existing.Tokens.Remove(ownershipToken);

                if (remaining.IsEmpty)
                {
                    if (((ICollection<KeyValuePair<string, OwnershipEntry>>)_ownership).Remove(
                        new KeyValuePair<string, OwnershipEntry>(key, existing)))
                    {
                        break;
                    }

                    continue;
                }

                var updated = new OwnershipEntry(existing.TenantName, remaining);

                if (_ownership.TryUpdate(key, updated, existing))
                {
                    break;
                }
            }
        }
    }

    /// <inheritdoc/>
    public bool IsOwnedByAnotherTenant(string baseUrl, string applicationName, string tenantName)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(applicationName))
        {
            return false;
        }

        var key = NormalizeKey(baseUrl, applicationName);

        if (_ownership.TryGetValue(key, out var existing))
        {
            return !string.Equals(existing.TenantName, tenantName, StringComparison.Ordinal);
        }

        return false;
    }

    private static string NormalizeKey(string baseUrl, string applicationName)
    {
        var normalizedBaseUrl = AsteriskSettingsUtilities.NormalizeBaseUrl(baseUrl);

        return normalizedBaseUrl.ToLowerInvariant() + "\u0000" + applicationName.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// An immutable ownership record pairing the owning tenant with the set of per-shell-generation
    /// tokens currently holding the claim. Immutability lets <see cref="ConcurrentDictionary{TKey, TValue}"/>
    /// compare-and-swap operations reason about a stable snapshot when adding or removing a token.
    /// </summary>
    private sealed class OwnershipEntry
    {
        public OwnershipEntry(string tenantName, ImmutableHashSet<string> tokens)
        {
            TenantName = tenantName;
            Tokens = tokens;
        }

        public string TenantName { get; }

        public ImmutableHashSet<string> Tokens { get; }
    }
}
