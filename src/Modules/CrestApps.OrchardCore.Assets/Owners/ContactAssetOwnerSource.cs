using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Assets.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Assets.Owners;

/// <summary>
/// Finds CRM contacts: content items of the types that carry the Omnichannel Contact part.
/// </summary>
internal sealed class ContactAssetOwnerSource : IAssetOwnerSource
{
    private readonly ISession _session;
    private readonly IOmnichannelContactTypeProvider _contactTypes;

    public ContactAssetOwnerSource(ISession session, IOmnichannelContactTypeProvider contactTypes)
    {
        _session = session;
        _contactTypes = contactTypes;
    }

    public AssetOwnerKind Kind
        => AssetOwnerKind.Contact;

    public async Task<IReadOnlyList<AssetOwnerOption>> SearchAsync(string query, int count, CancellationToken cancellationToken = default)
    {
        var types = (await _contactTypes.GetContactContentTypesAsync(cancellationToken)).ToArray();

        if (types.Length == 0)
        {
            return [];
        }

        var search = query?.Trim();

        var items = string.IsNullOrEmpty(search)
            ? _session.QueryIndex<ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(types))
            : _session.QueryIndex<ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(types) && index.DisplayText.Contains(search));

        return [.. (await items.OrderBy(index => index.DisplayText).Take(count).ListAsync(cancellationToken)).Select(item => new AssetOwnerOption(item.ContentItemId, item.DisplayText))];
    }

    public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var items = await _session.QueryIndex<ContentItemIndex>(index => index.Latest && index.ContentItemId.IsIn(ids)).ListAsync(cancellationToken);

        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            names.TryAdd(item.ContentItemId, item.DisplayText);
        }

        return names;
    }
}
