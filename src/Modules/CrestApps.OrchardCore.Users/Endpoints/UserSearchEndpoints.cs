using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.Users.Endpoints;

/// <summary>
/// The search behind the user pickers. It returns up to 50 <c>{ value, text }</c> items, the shape the shared item
/// selector reads, to users who hold <see cref="UserSearchPermissions.SearchUsers"/>.
/// </summary>
internal static class UserSearchEndpoints
{
    public const string RouteName = "CrestApps.Users.Search";

    /// <summary>
    /// Maps the user search endpoint.
    /// </summary>
    /// <param name="builder">The endpoint route builder.</param>
    public static IEndpointRouteBuilder AddUserSearchEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("Admin/api/crestapps/users/search", HandleAsync)
            .RequireAuthorization()
            .WithName(RouteName);

        return builder;
    }

    internal static async Task<IResult> HandleAsync(
        string query,
        string valueType,
        string[] role,
        string audience,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IUserSearchService userSearchService,
        CancellationToken cancellationToken)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, UserSearchPermissions.SearchUsers))
        {
            return Results.Forbid();
        }

        var users = await userSearchService.SearchAsync(new UserSearchQuery
        {
            Text = query,
            Roles = role ?? [],
            Audience = audience,
        }, cancellationToken);

        var items = users
            .Select(user => new UserSearchItem
            {
                Value = ResolveValue(user, valueType),
                Text = user.DisplayName,
                IsEnabled = user.IsEnabled,
            })
            .Where(static item => !string.IsNullOrWhiteSpace(item.Value))
            .OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Results.Ok<IEnumerable<UserSearchItem>>(items);
    }

    private static string ResolveValue(UserSearchResult user, string valueType)
    {
        return valueType?.Trim().ToLowerInvariant() switch
        {
            "username" => user.UserName,
            "normalizedusername" => user.NormalizedUserName,
            _ => user.UserId,
        };
    }
}
