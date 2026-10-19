using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Finds the contacts and accounts a lead may already be: contacts that share its phone number or email, and
/// accounts named after its company. Conversion offers them so a lead is merged into an existing customer instead
/// of creating a duplicate.
/// </summary>
public sealed class LeadMatchFinder
{
    private const int MaxMatches = 10;

    private readonly ISession _session;
    private readonly IContentManager _contentManager;
    private readonly OmnichannelContentTypeProvider _contentTypeProvider;
    private readonly IOmnichannelContactPhoneLookup _phoneLookup;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadMatchFinder"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="contentTypeProvider">The CRM content type provider.</param>
    /// <param name="phoneLookup">The lookup that finds records by any of their phone numbers.</param>
    public LeadMatchFinder(
        ISession session,
        IContentManager contentManager,
        OmnichannelContentTypeProvider contentTypeProvider,
        IOmnichannelContactPhoneLookup phoneLookup)
    {
        _session = session;
        _contentManager = contentManager;
        _contentTypeProvider = contentTypeProvider;
        _phoneLookup = phoneLookup;
    }

    /// <summary>
    /// Returns the contacts that share any of the lead's phone numbers or its primary email.
    /// </summary>
    /// <param name="lead">The lead.</param>
    public async Task<IReadOnlyList<ContentItem>> FindContactsAsync(ContentItem lead)
    {
        var contactTypes = (await _contentTypeProvider.GetContactKindContentTypesAsync()).ToArray();

        if (contactTypes.Length == 0)
        {
            return [];
        }

        var leadIndex = await _session.QueryIndex<OmnichannelContactIndex>(index =>
                index.ContentItemId == lead.ContentItemId && index.Latest)
            .FirstOrDefaultAsync();

        if (leadIndex is null)
        {
            return [];
        }

        var numbers = (await _phoneLookup.GetNumbersAsync([lead.ContentItemId], OmnichannelContactVersion.Latest))
            .Select(row => row.NormalizedPhoneNumber)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var email = leadIndex.PrimaryEmailAddress;
        var matchIds = new List<string>();

        if (numbers.Length > 0)
        {
            var byPhone = await _phoneLookup.FindByNumbersAsync(numbers, OmnichannelContactVersion.Latest, contactTypes);

            matchIds.AddRange(byPhone
                .Select(index => index.ContentItemId)
                .Where(id => id != lead.ContentItemId)
                .Distinct(StringComparer.Ordinal)
                .Take(MaxMatches));
        }

        if (!string.IsNullOrEmpty(email))
        {
            var byEmail = await _session.QueryIndex<OmnichannelContactIndex>(index =>
                    index.Latest &&
                    index.ContentType.IsIn(contactTypes) &&
                    index.PrimaryEmailAddress == email)
                .Take(MaxMatches)
                .ListAsync();

            matchIds.AddRange(byEmail.Select(index => index.ContentItemId));
        }

        var ids = matchIds
            .Where(id => !string.IsNullOrEmpty(id) && id != lead.ContentItemId)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxMatches)
            .ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        return (await _contentManager.GetAsync(ids, VersionOptions.Latest)).ToArray();
    }

    /// <summary>
    /// Returns the accounts whose name is the lead's company.
    /// </summary>
    /// <param name="company">The lead's company.</param>
    public async Task<IReadOnlyList<ContentItem>> FindAccountsAsync(string company)
    {
        if (string.IsNullOrWhiteSpace(company))
        {
            return [];
        }

        var accountTypes = (await _contentTypeProvider.GetAccountContentTypesAsync()).ToArray();

        if (accountTypes.Length == 0)
        {
            return [];
        }

        var name = company.Trim();

        return (await _session.Query<ContentItem, ContentItemIndex>(index =>
                index.Latest &&
                index.ContentType.IsIn(accountTypes) &&
                index.DisplayText == name)
            .Take(MaxMatches)
            .ListAsync())
            .ToArray();
    }
}
