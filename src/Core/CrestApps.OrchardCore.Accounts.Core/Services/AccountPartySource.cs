using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// Finds and names accounts (party kind <see cref="PartyKinds.Account"/>), the organizations of the Accounts module:
/// content items of its account types, by their display text. The id is the account's content item id.
/// </summary>
public sealed class AccountPartySource : IPartySource
{
    private readonly ISession _session;
    private readonly IAccountContentTypeProvider _accountTypes;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountPartySource"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="accountTypes">The account content types.</param>
    public AccountPartySource(ISession session, IAccountContentTypeProvider accountTypes)
    {
        _session = session;
        _accountTypes = accountTypes;
    }

    /// <inheritdoc/>
    public string Kind => PartyKinds.Account;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PartyOption>> SearchAsync(string query, int count, CancellationToken cancellationToken = default)
    {
        var types = (await _accountTypes.GetAccountTypesAsync()).Select(type => type.Name).ToArray();

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

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids is null || ids.Count == 0)
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
