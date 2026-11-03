using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Assets.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Assets.Owners;

/// <summary>
/// Finds CRM accounts, the companies and households of the CRM: content items of the types that carry the Account part.
/// </summary>
internal sealed class AccountAssetOwnerSource : IAssetOwnerSource
{
    private readonly ISession _session;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public AccountAssetOwnerSource(ISession session, IContentDefinitionManager contentDefinitionManager)
    {
        _session = session;
        _contentDefinitionManager = contentDefinitionManager;
    }

    public AssetOwnerKind Kind
        => AssetOwnerKind.Account;

    public async Task<IReadOnlyList<AssetOwnerOption>> SearchAsync(string query, int count, CancellationToken cancellationToken = default)
    {
        var types = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(type => type.Parts.Any(part => part.PartDefinition.Name == OmnichannelConstants.ContentParts.Account))
            .Select(type => type.Name)
            .ToArray();

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
