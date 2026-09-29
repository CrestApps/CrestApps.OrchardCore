using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents;
using OrchardCore.Lists.Indexes;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Endpoints;

/// <summary>
/// The searches behind the account and contact pickers of the CRM editors. Each returns up to 50
/// <c>{ value, text }</c> items, the shape the shared item selector reads.
/// </summary>
internal static class CrmSearchEndpoints
{
    private const int MaxResults = 50;

    /// <summary>
    /// Maps the account and contact search endpoints.
    /// </summary>
    /// <param name="builder">The endpoint route builder.</param>
    public static IEndpointRouteBuilder AddCrmSearchEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("Admin/api/crestapps/omnichannel/crm/accounts/search", SearchAccountsAsync)
            .RequireAuthorization()
            .WithName("CrestApps.Omnichannel.Crm.AccountSearch");

        builder.MapGet("Admin/api/crestapps/omnichannel/crm/contacts/search", SearchContactsAsync)
            .RequireAuthorization()
            .WithName("CrestApps.Omnichannel.Crm.ContactSearch");

        return builder;
    }

    internal static async Task<IResult> SearchAccountsAsync(
        string query,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        OmnichannelContentTypeProvider contentTypeProvider,
        global::YesSql.ISession session)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, CommonPermissions.ListContent))
        {
            return Results.Forbid();
        }

        var accountTypes = (await contentTypeProvider.GetAccountContentTypesAsync()).ToArray();

        if (accountTypes.Length == 0)
        {
            return Results.Ok(Array.Empty<object>());
        }

        var search = query?.Trim();

        var accountQuery = string.IsNullOrEmpty(search)
            ? session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(accountTypes))
            : session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(accountTypes) && index.DisplayText.Contains(search));

        var items = await accountQuery
            .OrderBy(index => index.DisplayText)
            .Take(MaxResults)
            .ListAsync();

        return Results.Ok(items.Select(item => new { value = item.ContentItemId, text = item.DisplayText }));
    }

    internal static async Task<IResult> SearchContactsAsync(
        string query,
        string accountId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        OmnichannelContentTypeProvider contentTypeProvider,
        global::YesSql.ISession session)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, CommonPermissions.ListContent))
        {
            return Results.Forbid();
        }

        var contactTypes = (await contentTypeProvider.GetContactKindContentTypesAsync()).ToArray();

        if (contactTypes.Length == 0)
        {
            return Results.Ok(Array.Empty<object>());
        }

        var search = query?.Trim();

        IQuery<ContentItem> contentQuery = string.IsNullOrEmpty(search)
            ? session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(contactTypes))
            : session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(contactTypes) && index.DisplayText.Contains(search));

        if (!string.IsNullOrWhiteSpace(accountId))
        {
            contentQuery = contentQuery.With<ContainedPartIndex>(index => index.ListContentItemId == accountId);
        }

        var items = await contentQuery
            .With<ContentItemIndex>()
            .OrderBy(index => index.DisplayText)
            .Take(MaxResults)
            .ListAsync();

        return Results.Ok(items.Select(item => new { value = item.ContentItemId, text = item.DisplayText }));
    }
}
