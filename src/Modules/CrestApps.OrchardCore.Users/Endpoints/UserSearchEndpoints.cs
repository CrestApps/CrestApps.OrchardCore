using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Security;
using OrchardCore.Users;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Users.Endpoints;

internal static class UserSearchEndpoints
{
    public static IEndpointRouteBuilder AddUserSearchEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("Admin/api/crestapps/users/search", HandleAsync)
            .RequireAuthorization();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        string query,
        string valueType,
        string[] role,
        RoleManager<IRole> roleManager,
        UserManager<IUser> userManager,
        YSession session,
        IDisplayNameProvider displayNameProvider)
    {
        var searchQuery = session.Query<User>();

        if (role is { Length: > 0 })
        {
            var normalizedRoles = role
                .Where(static roleName => !string.IsNullOrWhiteSpace(roleName))
                .Select(roleManager.NormalizeKey)
                .Where(static roleName => !string.IsNullOrWhiteSpace(roleName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (normalizedRoles.Length > 0)
            {
                searchQuery = searchQuery.With<UserByRoleNameIndex>(x => x.RoleName.IsIn(normalizedRoles));
            }
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            searchQuery = searchQuery.With<UserIndex>(x => x.IsEnabled);
        }
        else
        {
            var normalizedQuery = userManager.NormalizeName(query);
            searchQuery = searchQuery.With<UserIndex>(x => x.IsEnabled && x.NormalizedUserName.Contains(normalizedQuery));
        }

        var users = await searchQuery.Take(50).ListAsync();
        var items = new List<UserSearchItem>(users.Count());

        foreach (var user in users)
        {
            items.Add(new UserSearchItem
            {
                Value = ResolveValue(user, valueType),
                Text = await displayNameProvider.GetAsync(user),
                IsEnabled = user.IsEnabled,
            });
        }

        return Results.Ok(items
            .Where(static item => !string.IsNullOrWhiteSpace(item.Value))
            .OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase));
    }

    private static string ResolveValue(User user, string valueType)
    {
        return valueType?.Trim().ToLowerInvariant() switch
        {
            "username" => user.UserName,
            "normalizedusername" => user.NormalizedUserName,
            _ => user.UserId,
        };
    }
}

internal sealed class UserSearchItem
{
    public string Value { get; set; }

    public string Text { get; set; }

    public string SecondaryText { get; set; }

    public bool IsEnabled { get; set; }
}
