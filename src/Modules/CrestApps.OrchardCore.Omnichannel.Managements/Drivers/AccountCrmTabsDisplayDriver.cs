using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Lists.Indexes;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Adds the CRM's tabs to an account's page: its contacts, its opportunities, the leads that became its contacts, and
/// its contacts' activities. A tab shows only the types its user may view.
/// </summary>
internal sealed class AccountCrmTabsDisplayDriver : ContentDisplayDriver
{
    private const int MaxRows = 25;

    private readonly ISession _session;
    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly INamedCatalog<OpportunityStage> _stages;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccountCrmTabsDisplayDriver(
        ISession session,
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        INamedCatalog<OpportunityStage> stages,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _session = session;
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _stages = stages;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override bool CanHandleModel(ContentItem contentItem)
        => contentItem.Has<AccountPart>();

    public override async Task<IDisplayResult> DisplayAsync(ContentItem contentItem, BuildDisplayContext context)
    {
        if (context.DisplayType != OrchardCoreConstants.DisplayType.DetailAdmin ||
            _httpContextAccessor.HttpContext?.User is not { } user)
        {
            return null;
        }

        var definitions = await _contentDefinitionManager.ListTypeDefinitionsAsync();
        var contactTypes = await ViewableAsync(user, definitions.Where(OmnichannelRecordKinds.IsContact));
        var opportunityTypes = await ViewableAsync(user, definitions.Where(OmnichannelRecordKinds.IsOpportunity));
        var leadTypes = await ViewableAsync(user, definitions.Where(OmnichannelRecordKinds.IsLead));
        var results = new List<IDisplayResult>();

        if (contactTypes.Count > 0)
        {
            results.Add(Initialize<AccountContactsTabViewModel>("AccountContactsTab", async model =>
            {
                model.Account = contentItem;
                model.CreatableTypes = await CreatableAsync(user, contactTypes);
                (model.Count, model.Contacts) = await GetContactsAsync(contentItem.ContentItemId, contactTypes);
            }).Location(OrchardCoreConstants.DisplayType.DetailAdmin, AccountPageTabs.Location(AccountPageTabs.Contacts)));
        }

        if (opportunityTypes.Count > 0)
        {
            results.Add(Initialize<AccountOpportunitiesTabViewModel>("AccountOpportunitiesTab", async model =>
            {
                model.Account = contentItem;
                model.CreatableTypes = await CreatableAsync(user, opportunityTypes);
                (model.Count, model.Opportunities) = await GetOpportunitiesAsync(contentItem.ContentItemId, opportunityTypes);
            }).Location(OrchardCoreConstants.DisplayType.DetailAdmin, AccountPageTabs.Location(AccountPageTabs.Opportunities)));
        }

        if (leadTypes.Count > 0 && contactTypes.Count > 0)
        {
            results.Add(Initialize<AccountLeadsTabViewModel>("AccountLeadsTab", async model =>
            {
                (model.Count, model.Leads) = await GetConvertedLeadsAsync(contentItem.ContentItemId, leadTypes);
            }).Location(OrchardCoreConstants.DisplayType.DetailAdmin, AccountPageTabs.Location(AccountPageTabs.Leads)));
        }

        if (await _authorizationService.AuthorizeAsync(user, OmnichannelConstants.Permissions.ListContactActivities, contentItem))
        {
            results.Add(Initialize<AccountActivitiesTabViewModel>("AccountActivitiesTab", async model =>
            {
                model.AccountContentItemId = contentItem.ContentItemId;
                await PopulateActivitiesAsync(model, contentItem.ContentItemId);
            }).Location(OrchardCoreConstants.DisplayType.DetailAdmin, AccountPageTabs.Location(AccountPageTabs.Activities)));
        }

        return results.Count == 0 ? null : Combine(results);
    }

    private async Task<(int Count, IReadOnlyList<AccountContactRow> Rows)> GetContactsAsync(string accountId, IReadOnlyList<ContentTypeDefinition> types)
    {
        var typeNames = types.Select(type => type.Name).ToArray();
        var query = _session.Query<ContentItem, ContainedPartIndex>(index => index.ListContentItemId == accountId)
            .With<ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(typeNames));

        var count = await query.CountAsync();
        var contacts = (await query.OrderBy(index => index.DisplayText).Take(MaxRows).ListAsync()).ToArray();

        if (contacts.Length == 0)
        {
            return (count, []);
        }

        var ids = contacts.Select(contact => contact.ContentItemId).ToArray();
        var details = (await _session.QueryIndex<OmnichannelContactIndex>(index => index.Latest && index.ContentItemId.IsIn(ids)).ListAsync())
            .GroupBy(index => index.ContentItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        return (count, [.. contacts.Select(contact =>
        {
            details.TryGetValue(contact.ContentItemId, out var detail);

            return new AccountContactRow(contact, detail?.PrimaryEmailAddress, detail?.PrimaryCellPhoneNumber ?? detail?.PrimaryHomePhoneNumber);
        })]);
    }

    private async Task<(int Count, IReadOnlyList<AccountOpportunityRow> Rows)> GetOpportunitiesAsync(string accountId, IReadOnlyList<ContentTypeDefinition> types)
    {
        var typeNames = types.Select(type => type.Name).ToArray();
        var query = _session.QueryIndex<OpportunityIndex>(index =>
            index.AccountContentItemId == accountId &&
            index.Latest &&
            index.ContentType.IsIn(typeNames));

        var count = await query.CountAsync();

        // Open deals first, the soonest to close at the top.
        var rows = (await query.OrderBy(index => index.IsClosed).ThenBy(index => index.CloseDate).Take(MaxRows).ListAsync()).ToArray();

        if (rows.Length == 0)
        {
            return (count, []);
        }

        var items = (await _contentManager.GetAsync(rows.Select(row => row.ContentItemId).ToArray(), VersionOptions.Latest))
            .ToDictionary(item => item.ContentItemId, StringComparer.Ordinal);
        var stages = (await _stages.GetAllAsync()).ToDictionary(stage => stage.ItemId, stage => stage.Name, StringComparer.Ordinal);

        return (count, [.. rows
            .Where(row => items.ContainsKey(row.ContentItemId))
            .Select(row => new AccountOpportunityRow(
                items[row.ContentItemId],
                row.StageId is not null && stages.TryGetValue(row.StageId, out var stage) ? stage : null,
                row.Amount,
                row.CloseDate,
                row.IsClosed,
                row.IsWon))]);
    }

    private async Task<(int Count, IReadOnlyList<AccountLeadRow> Rows)> GetConvertedLeadsAsync(string accountId, IReadOnlyList<ContentTypeDefinition> types)
    {
        // A lead never sits in an account; it gets one when it is converted, into a contact the account holds.
        var contactIds = (await _session.QueryIndex<ContainedPartIndex>(index => index.ListContentItemId == accountId).ListAsync())
            .Select(index => index.ContentItemId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (contactIds.Length == 0)
        {
            return (0, []);
        }

        var typeNames = types.Select(type => type.Name).ToArray();
        var query = _session.QueryIndex<LeadIndex>(index =>
            index.Latest &&
            index.IsConverted &&
            index.ContentType.IsIn(typeNames) &&
            index.ConvertedContactItemId.IsIn(contactIds));

        var count = await query.CountAsync();
        var rows = (await query.OrderByDescending(index => index.ConvertedUtc).Take(MaxRows).ListAsync()).ToArray();

        if (rows.Length == 0)
        {
            return (count, []);
        }

        var items = (await _contentManager.GetAsync(rows.Select(row => row.ContentItemId).ToArray(), VersionOptions.Latest))
            .ToDictionary(item => item.ContentItemId, StringComparer.Ordinal);

        return (count, [.. rows
            .Where(row => items.ContainsKey(row.ContentItemId))
            .Select(row => new AccountLeadRow(items[row.ContentItemId], row.ConvertedContactItemId, row.ConvertedUtc))]);
    }

    private async Task PopulateActivitiesAsync(AccountActivitiesTabViewModel model, string accountId)
    {
        // An account's activities are those of the contacts it holds, as on the account's activity page.
        var contactIds = (await _session.QueryIndex<ContainedPartIndex>(index => index.ListContentItemId == accountId).ListAsync())
            .Select(index => index.ContentItemId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (contactIds.Length == 0)
        {
            return;
        }

        var scheduled = _session.QueryIndex<OmnichannelActivityIndex>(index =>
            index.ContactContentItemId.IsIn(contactIds) &&
            index.Status == ActivityStatus.NotStated &&
            index.InteractionType == ActivityInteractionType.Manual,
            collection: OmnichannelConstants.CollectionName);

        var completed = _session.QueryIndex<OmnichannelActivityIndex>(index =>
            index.ContactContentItemId.IsIn(contactIds) &&
            index.Status == ActivityStatus.Completed,
            collection: OmnichannelConstants.CollectionName);

        model.ScheduledCount = await scheduled.CountAsync();
        model.CompletedCount = await completed.CountAsync();
        model.NextScheduledUtc = model.ScheduledCount == 0 ? null : (await scheduled.OrderBy(index => index.ScheduledUtc).FirstOrDefaultAsync())?.ScheduledUtc;
        model.LastCompletedUtc = model.CompletedCount == 0 ? null : (await completed.OrderByDescending(index => index.CompletedUtc).FirstOrDefaultAsync())?.CompletedUtc;
    }

    private async Task<IReadOnlyList<ContentTypeDefinition>> ViewableAsync(ClaimsPrincipal user, IEnumerable<ContentTypeDefinition> types)
    {
        var viewable = new List<ContentTypeDefinition>();

        foreach (var type in types)
        {
            if (await _authorizationService.AuthorizeAsync(user, CommonPermissions.ViewContent, new ContentItem { ContentType = type.Name }))
            {
                viewable.Add(type);
            }
        }

        return viewable;
    }

    private async Task<IReadOnlyList<ContentTypeDefinition>> CreatableAsync(ClaimsPrincipal user, IReadOnlyList<ContentTypeDefinition> types)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var creatable = new List<ContentTypeDefinition>();

        foreach (var type in types)
        {
            if (await _authorizationService.AuthorizeContentTypeAsync(user, CommonPermissions.EditContent, type.Name, userId))
            {
                creatable.Add(type);
            }
        }

        return creatable;
    }
}
