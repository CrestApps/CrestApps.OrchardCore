using CrestApps.OrchardCore.Accounts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Accounts.Endpoints;

/// <summary>
/// The search behind the account pickers. It returns up to 50 <c>{ value, text }</c> items, the shape the shared item
/// selector reads.
/// </summary>
internal static class AccountSearchEndpoints
{
    internal const string Pattern = "Admin/api/crestapps/accounts/search";

    private const int MaxResults = 50;

    /// <summary>
    /// Maps the account search endpoint.
    /// </summary>
    /// <param name="builder">The endpoint route builder.</param>
    public static IEndpointRouteBuilder AddAccountSearchEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(Pattern, SearchAsync)
            .RequireAuthorization()
            .WithName(AccountsConstants.RouteNames.Search);

        return builder;
    }

    internal static async Task<IResult> SearchAsync(
        string query,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAccountContentTypeProvider accountContentTypeProvider,
        global::YesSql.ISession session)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, CommonPermissions.ListContent))
        {
            return Results.Forbid();
        }

        // Only the account types the user may view are searched: an account type is securable, so listing content in
        // general doesn't let a user see every account's name.
        var accountTypes = new List<string>();

        foreach (var type in await accountContentTypeProvider.GetAccountTypesAsync())
        {
            if (await authorizationService.AuthorizeAsync(httpContext.User, CommonPermissions.ViewContent, new ContentItem { ContentType = type.Name }))
            {
                accountTypes.Add(type.Name);
            }
        }

        if (accountTypes.Count == 0)
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
}
