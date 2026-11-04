using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Assets.Services;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Assets.Owners;

/// <summary>
/// Customers are users: finds them by user name or email.
/// </summary>
internal sealed class CustomerAssetOwnerSource : IAssetOwnerSource
{
    private readonly ISession _session;

    public CustomerAssetOwnerSource(ISession session)
    {
        _session = session;
    }

    public AssetOwnerKind Kind
        => AssetOwnerKind.Customer;

    public async Task<IReadOnlyList<AssetOwnerOption>> SearchAsync(string query, int count, CancellationToken cancellationToken = default)
    {
        var normalized = query?.Trim().ToUpperInvariant();

        var users = string.IsNullOrEmpty(normalized)
            ? _session.Query<User, UserIndex>(index => index.IsEnabled)
            : _session.Query<User, UserIndex>(index => index.IsEnabled && (index.NormalizedUserName.Contains(normalized) || index.NormalizedEmail.Contains(normalized)));

        return [.. (await users.OrderBy(index => index.NormalizedUserName).Take(count).ListAsync(cancellationToken)).Select(ToOption)];
    }

    public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var users = await _session.Query<User, UserIndex>(index => index.UserId.IsIn(ids)).ListAsync(cancellationToken);

        return users.Select(ToOption).ToDictionary(option => option.Id, option => option.Name, StringComparer.Ordinal);
    }

    private static AssetOwnerOption ToOption(User user)
        => new(user.UserId, string.IsNullOrEmpty(user.Email) ? user.UserName : $"{user.UserName} ({user.Email})");
}
