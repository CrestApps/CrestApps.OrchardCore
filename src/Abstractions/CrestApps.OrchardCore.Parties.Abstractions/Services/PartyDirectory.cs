using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Parties.Services;

/// <summary>
/// The default <see cref="IPartyDirectory"/>: the first source registered for a kind answers for it. Kinds compare
/// without case.
/// </summary>
public sealed class PartyDirectory : IPartyDirectory
{
    private readonly Dictionary<string, IPartySource> _sources = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _kinds = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyDirectory"/> class.
    /// </summary>
    /// <param name="sources">The party sources.</param>
    public PartyDirectory(IEnumerable<IPartySource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        foreach (var source in sources)
        {
            if (!string.IsNullOrEmpty(source?.Kind) && _sources.TryAdd(source.Kind, source))
            {
                _kinds.Add(source.Kind);
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Kinds => _kinds;

    /// <inheritdoc/>
    public IPartySource GetSource(string kind)
        => string.IsNullOrEmpty(kind) ? null : _sources.GetValueOrDefault(kind);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PartyOption>> SearchAsync(string kind, string query, int count, CancellationToken cancellationToken = default)
        => GetSource(kind) is { } source ? await source.SearchAsync(query, count, cancellationToken) : [];

    /// <inheritdoc/>
    public async Task<string> GetNameAsync(PartyRef party, CancellationToken cancellationToken = default)
    {
        if (party.IsEmpty || GetSource(party.Kind) is not { } source)
        {
            return null;
        }

        return (await source.GetNamesAsync([party.Id], cancellationToken)).GetValueOrDefault(party.Id);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<PartyRef, string>> GetNamesAsync(IEnumerable<PartyRef> parties, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parties);

        var names = new Dictionary<PartyRef, string>();

        foreach (var group in parties.Where(party => !party.IsEmpty).GroupBy(party => party.Kind, StringComparer.OrdinalIgnoreCase))
        {
            if (GetSource(group.Key) is not { } source)
            {
                continue;
            }

            var found = await source.GetNamesAsync([.. group.Select(party => party.Id).Distinct(StringComparer.Ordinal)], cancellationToken);

            foreach (var party in group)
            {
                if (found.TryGetValue(party.Id, out var name))
                {
                    names[party] = name;
                }
            }
        }

        return names;
    }
}
