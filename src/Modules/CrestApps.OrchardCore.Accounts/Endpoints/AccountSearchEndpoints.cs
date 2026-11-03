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

        var accountTypes = (await accountContentTypeProvider.GetAccountTypesAsync())
            .Select(type => type.Name)
            .ToArray();

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
}
