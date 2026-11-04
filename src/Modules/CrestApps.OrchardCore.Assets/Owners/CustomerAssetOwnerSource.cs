using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Assets.Services;
using CrestApps.OrchardCore.Users;

namespace CrestApps.OrchardCore.Assets.Owners;

/// <summary>
/// Customers are users: finds them by user name or email through the shared <see cref="IUserSearchService"/>.
/// </summary>
internal sealed class CustomerAssetOwnerSource : IAssetOwnerSource
{
    private readonly IUserSearchService _userSearchService;

    public CustomerAssetOwnerSource(IUserSearchService userSearchService)
    {
        _userSearchService = userSearchService;
    }

    public AssetOwnerKind Kind
        => AssetOwnerKind.Customer;

    public async Task<IReadOnlyList<AssetOwnerOption>> SearchAsync(string query, int count, CancellationToken cancellationToken = default)
    {
        var users = await _userSearchService.SearchAsync(new UserSearchQuery
        {
            Text = query,
            MatchEmail = true,
            MaxResults = count,
        }, cancellationToken);

        return [.. users.Select(ToOption)];
    }

    public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var users = await _userSearchService.GetAsync(ids, cancellationToken);

        return users.Select(ToOption).ToDictionary(option => option.Id, option => option.Name, StringComparer.Ordinal);
    }

    private static AssetOwnerOption ToOption(UserSearchResult user)
        => new(user.UserId, string.IsNullOrEmpty(user.Email) ? user.UserName : $"{user.UserName} ({user.Email})");
}
