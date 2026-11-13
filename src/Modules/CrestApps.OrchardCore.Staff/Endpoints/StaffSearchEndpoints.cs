using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.Staff.Endpoints;

/// <summary>
/// The search behind the staff picker: active staff members as <c>{ value, text }</c> items.
/// </summary>
internal static class StaffSearchEndpoints
{
    public const string RouteName = "CrestApps.Staff.Search";

    public static IEndpointRouteBuilder AddStaffSearchEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("Admin/api/staff/search", HandleAsync)
            .RequireAuthorization()
            .WithName(RouteName);

        return builder;
    }

    internal static async Task<IResult> HandleAsync(
        string query,
        string capability,
        string valueType,
        string excludeId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IStaffDirectory directory,
        CancellationToken cancellationToken)
    {
        // Choosing a person is part of editing records in many modules, so anyone who may search users may search staff.
        if (!await authorizationService.AuthorizeAsync(httpContext.User, StaffPermissions.ViewStaff)
            && !await authorizationService.AuthorizeAsync(httpContext.User, UserSearchPermissions.SearchUsers))
        {
            return Results.Forbid();
        }

        var byUser = string.Equals(valueType, "userId", StringComparison.OrdinalIgnoreCase);
        var people = await directory.SearchAsync(query, capability, requireUser: byUser, take: 30, cancellationToken);

        var items = people
            .Where(person => string.IsNullOrEmpty(excludeId) || person.ItemId != excludeId)
            .Select(person => new StaffSearchItem
            {
                Value = byUser ? person.UserId : person.ItemId,
                Text = string.IsNullOrEmpty(person.JobTitle) ? person.DisplayName : $"{person.DisplayName} ({person.JobTitle})",
                IsEnabled = true,
            })
            .Where(item => !string.IsNullOrEmpty(item.Value))
            .ToArray();

        return Results.Ok<IEnumerable<StaffSearchItem>>(items);
    }

    internal sealed class StaffSearchItem
    {
        public string Value { get; set; }

        public string Text { get; set; }

        public bool IsEnabled { get; set; }
    }
}
