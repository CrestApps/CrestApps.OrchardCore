using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;

namespace CrestApps.OrchardCore.Users.Core.Services;

/// <summary>
/// A user (party kind <see cref="PartyKinds.User"/>) acts for themselves only: the member of a user party is that user,
/// and the user party a user acts for is their own.
/// </summary>
public sealed class UserPartyMemberSource : IPartyMemberSource
{
    /// <inheritdoc/>
    public string Kind => PartyKinds.User;

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GetMemberUserIdsAsync(PartyRef party, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(party.IsEmpty ? [] : [party.Id]);

    /// <inheritdoc/>
    public Task<IReadOnlyList<PartyRef>> GetPartiesAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PartyRef>>(string.IsNullOrEmpty(userId) ? [] : [new PartyRef(PartyKinds.User, userId)]);
}
