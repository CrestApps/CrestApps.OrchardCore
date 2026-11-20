using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Parties.Services;

/// <summary>
/// Reads every party a signed-in user is.
/// </summary>
public static class UserParties
{
    /// <summary>
    /// Returns the user party of a user, then every party the <see cref="IUserPartySource"/>s say the user is.
    /// </summary>
    /// <param name="sources">The sources the enabled features registered.</param>
    /// <param name="userId">The user's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The parties, without repeats; empty when <paramref name="userId"/> is empty.</returns>
    public static async Task<IReadOnlyList<PartyRef>> GetAsync(IEnumerable<IUserPartySource> sources, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);

        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var parties = new List<PartyRef> { new(PartyKinds.User, userId) };

        foreach (var source in sources)
        {
            foreach (var party in await source.GetPartiesAsync(userId, cancellationToken))
            {
                if (!party.IsEmpty && !parties.Contains(party))
                {
                    parties.Add(party);
                }
            }
        }

        return parties;
    }
}
