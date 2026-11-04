using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Assets.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Assets.Owners;

/// <summary>
/// Finds accounts, the organizations of the Accounts module: content items of its account types.
/// </summary>
internal sealed class AccountAssetOwnerSource : IAssetOwnerSource
{
    private readonly ISession _session;
    private readonly IAccountContentTypeProvider _accountTypes;

    public AccountAssetOwnerSource(ISession session, IAccountContentTypeProvider accountTypes)
    {
        _session = session;
        _accountTypes = accountTypes;
    }

    public AssetOwnerKind Kind
        => AssetOwnerKind.Account;

    public async Task<IReadOnlyList<AssetOwnerOption>> SearchAsync(string query, int count, CancellationToken cancellationToken = default)
    {
        var types = (await _accountTypes.GetAccountTypesAsync()).Select(type => type.Name).ToArray();

        if (types.Length == 0)
        {
            return [];
        }

        var search = query?.Trim();

        var items = string.IsNullOrEmpty(search)
            ? _session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(types))
            : _session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(types) && index.DisplayText.Contains(search));

        return [.. (await items.OrderBy(index => index.DisplayText).Take(count).ListAsync(cancellationToken)).Select(item => new AssetOwnerOption(item.ContentItemId, item.DisplayText))];
    }

    public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var items = await _session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentItemId.IsIn(ids)).ListAsync(cancellationToken);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            names.TryAdd(item.ContentItemId, item.DisplayText);
        }

        return names;
    }
}
