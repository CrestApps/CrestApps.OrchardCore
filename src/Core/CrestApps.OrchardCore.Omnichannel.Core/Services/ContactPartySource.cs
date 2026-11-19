using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Finds and names CRM contacts (party kind <see cref="PartyKinds.Contact"/>): content items of the types that carry the
/// Omnichannel Contact part. A contact is reached at its first e-mail address and its preferred phone number.
/// </summary>
public sealed class ContactPartySource : IPartySource
{
    private readonly ISession _session;
    private readonly IContentManager _contentManager;
    private readonly IOmnichannelContactTypeProvider _contactTypes;
    private readonly IContactMethodsAccessor _contactMethods;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactPartySource"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="contactTypes">The contact content types.</param>
    /// <param name="contactMethods">Reads a contact's e-mail addresses and phone numbers.</param>
    public ContactPartySource(
        ISession session,
        IContentManager contentManager,
        IOmnichannelContactTypeProvider contactTypes,
        IContactMethodsAccessor contactMethods)
    {
        _session = session;
        _contentManager = contentManager;
        _contactTypes = contactTypes;
        _contactMethods = contactMethods;
    }

    /// <inheritdoc/>
    public string Kind => PartyKinds.Contact;

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, ContactInfo>> GetContactInfoAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        var contacts = new Dictionary<string, ContactInfo>(StringComparer.Ordinal);

        if (ids is null || ids.Count == 0)
        {
            return contacts;
        }

        foreach (var item in await _contentManager.GetAsync(ids, VersionOptions.Latest))
        {
            var methods = _contactMethods.Get(item);

            contacts.TryAdd(
                item.ContentItemId,
                ContactInfo.From(item.DisplayText, methods.GetPrimaryEmailAddress(), methods.GetPreferredPhoneNumber()?.PhoneNumber));
        }

        return contacts;
    }
}
