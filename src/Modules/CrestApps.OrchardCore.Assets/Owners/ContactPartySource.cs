using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Assets.Owners;

/// <summary>
/// Finds and names CRM contacts (party kind <see cref="PartyKinds.Contact"/>): content items of the types that carry the
/// Omnichannel Contact part.
/// </summary>
internal sealed class ContactPartySource : IPartySource
{
    private readonly ISession _session;
    private readonly IOmnichannelContactTypeProvider _contactTypes;

    public ContactPartySource(ISession session, IOmnichannelContactTypeProvider contactTypes)
    {
        _session = session;
        _contactTypes = contactTypes;
    }

    public string Kind
        => PartyKinds.Contact;

    public async Task<IReadOnlyList<PartyOption>> SearchAsync(string query, int count, CancellationToken cancellationToken = default)
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

        return [.. (await items.OrderBy(index => index.DisplayText).Take(count).ListAsync(cancellationToken)).Select(item => new PartyOption(item.ContentItemId, item.DisplayText))];
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
