using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;

namespace CrestApps.OrchardCore.Users.Core.Services;

/// <summary>
/// Finds and names users (party kind <see cref="PartyKinds.User"/>) through the shared <see cref="IUserSearchService"/>,
/// by user name or e-mail, named by their display name from the <see cref="IDisplayNameProvider"/>.
/// </summary>
public sealed class UserPartySource : IPartySource
{
    private readonly IUserSearchService _userSearchService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserPartySource"/> class.
    /// </summary>
    /// <param name="userSearchService">The user search service.</param>
    public UserPartySource(IUserSearchService userSearchService)
    {
        _userSearchService = userSearchService;
    }

    /// <inheritdoc/>
    public string Kind => PartyKinds.User;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PartyOption>> SearchAsync(string query, int count, CancellationToken cancellationToken = default)
    {
        var users = await _userSearchService.SearchAsync(new UserSearchQuery
        {
            Text = query,
            MatchEmail = true,
            MaxResults = count,
        }, cancellationToken);

        return [.. users.Select(ToOption)];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids is null || ids.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var users = await _userSearchService.GetAsync(ids, cancellationToken);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var option in users.Select(ToOption))
        {
            names.TryAdd(option.Id, option.Name);
        }

        return names;
    }

    private static PartyOption ToOption(UserSearchResult user)
    {
        var name = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName;

        return new(user.UserId, string.IsNullOrEmpty(user.Email) ? name : $"{name} ({user.Email})");
    }
}
