using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadMatchFinder"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="contentTypeProvider">The CRM content type provider.</param>
    public LeadMatchFinder(
        ISession session,
        IContentManager contentManager,
        OmnichannelContentTypeProvider contentTypeProvider)
    {
        _session = session;
        _contentManager = contentManager;
        _contentTypeProvider = contentTypeProvider;
    }

    /// <summary>
    /// Returns the contacts that share the lead's primary cell or home number or its primary email.
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

        var numbers = new[] { leadIndex.NormalizedPrimaryCellPhoneNumber, leadIndex.NormalizedPrimaryHomePhoneNumber }
            .Where(number => !string.IsNullOrEmpty(number))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var email = leadIndex.PrimaryEmailAddress;
        var matchIds = new List<string>();

        if (numbers.Length > 0)
        {
            var byPhone = await _session.QueryIndex<OmnichannelContactIndex>(index =>
                    index.Latest &&
                    index.ContentType.IsIn(contactTypes) &&
                    (index.NormalizedPrimaryCellPhoneNumber.IsIn(numbers) || index.NormalizedPrimaryHomePhoneNumber.IsIn(numbers)))
                .Take(MaxMatches)
                .ListAsync();

            matchIds.AddRange(byPhone.Select(index => index.ContentItemId));
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
