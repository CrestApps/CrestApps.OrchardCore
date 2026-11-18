using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Parties.Services;

/// <summary>
/// The default <see cref="IPartyMembershipService"/>: it asks every <see cref="IPartyMemberSource"/> of the party's kind,
/// and treats a user as the one member of their own user party, with every access level. Kinds compare without case; user
/// ids compare exactly.
/// </summary>
public sealed class PartyMembershipService : IPartyMembershipService
{
    private readonly IEnumerable<IPartyMemberSource> _sources;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyMembershipService"/> class.
    /// </summary>
    /// <param name="sources">The member sources, in registration order.</param>
    public PartyMembershipService(IEnumerable<IPartyMemberSource> sources)
    {
        _sources = sources ?? [];
    }

    /// <inheritdoc/>
    public Task<bool> IsMemberAsync(PartyRef party, string userId, CancellationToken cancellationToken = default)
        => IsMemberAsync(party, userId, PartyAccess.Member, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> IsMemberAsync(PartyRef party, string userId, PartyAccess access, CancellationToken cancellationToken = default)
    {
        if (party.IsEmpty || string.IsNullOrEmpty(userId))
        {
            return false;
        }

        if (IsUser(party) && string.Equals(party.Id, userId, StringComparison.Ordinal))
        {
            return true;
        }

        var members = await GetMemberUserIdsAsync(party, access, cancellationToken);

        return members.Contains(userId, StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GetMemberUserIdsAsync(PartyRef party, CancellationToken cancellationToken = default)
        => GetMemberUserIdsAsync(party, PartyAccess.Member, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetMemberUserIdsAsync(PartyRef party, PartyAccess access, CancellationToken cancellationToken = default)
    {
        if (party.IsEmpty)
        {
            return [];
        }

        var members = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (IsUser(party) && seen.Add(party.Id))
        {
            members.Add(party.Id);
        }

        foreach (var source in SourcesOf(party.Kind))
        {
            foreach (var userId in await source.GetMemberUserIdsAsync(party, access, cancellationToken) ?? [])
            {
                if (!string.IsNullOrEmpty(userId) && seen.Add(userId))
                {
                    members.Add(userId);
                }
            }
        }

        return members;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PartyRef>> GetPartiesAsync(string userId, CancellationToken cancellationToken = default)
        => GetPartiesAsync(userId, PartyAccess.Member, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PartyRef>> GetPartiesAsync(string userId, PartyAccess access, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var self = new PartyRef(PartyKinds.User, userId);
        var parties = new List<PartyRef> { self };
        var seen = new HashSet<PartyRef>(PartyRefComparer.Instance) { self };

        foreach (var source in _sources)
        {
            if (string.IsNullOrEmpty(source?.Kind))
            {
                continue;
            }

            foreach (var party in await source.GetPartiesAsync(userId, access, cancellationToken) ?? [])
            {
                if (!party.IsEmpty && seen.Add(party))
                {
                    parties.Add(party);
                }
            }
        }

        return parties;
    }

    private IEnumerable<IPartyMemberSource> SourcesOf(string kind)
        => _sources.Where(source => source is not null && string.Equals(source.Kind, kind, StringComparison.OrdinalIgnoreCase));

    private static bool IsUser(PartyRef party)
        => string.Equals(party.Kind, PartyKinds.User, StringComparison.OrdinalIgnoreCase);

    // Two references to the same party match whatever the case of their kind.
    private sealed class PartyRefComparer : IEqualityComparer<PartyRef>
    {
        public static readonly PartyRefComparer Instance = new();

        public bool Equals(PartyRef x, PartyRef y)
            => string.Equals(x.Kind, y.Kind, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Id, y.Id, StringComparison.Ordinal);

        public int GetHashCode(PartyRef obj)
            => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Kind ?? string.Empty), StringComparer.Ordinal.GetHashCode(obj.Id ?? string.Empty));
    }
}
