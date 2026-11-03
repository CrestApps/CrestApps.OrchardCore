using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Calendars.Drivers;

/// <summary>
/// The Sharing card of the calendar editor, shown to people who manage the calendar.
/// </summary>
internal sealed class CalendarSharingDisplayDriver : DisplayDriver<Calendar>
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRoleService _roleService;
    private readonly CalendarUserLookup _userLookup;
    private readonly ICalendarAccessService _accessService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CalendarSharingDisplayDriver(
        IRoleService roleService,
        CalendarUserLookup userLookup,
        ICalendarAccessService accessService,
        IHttpContextAccessor httpContextAccessor)
    {
        _roleService = roleService;
        _userLookup = userLookup;
        _accessService = accessService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IDisplayResult> EditAsync(Calendar calendar, BuildEditorContext context)
    {
        if (!context.IsNew && await _accessService.GetAccessLevelAsync(_httpContextAccessor.HttpContext?.User, calendar) < CalendarAccessLevel.Manage)
        {
            return null;
        }

        var userIds = calendar.Shares.Where(share => share.PrincipalKind == CalendarPrincipalKind.User).Select(share => share.PrincipalId).ToList();
        var users = (await _userLookup.GetAsync(userIds)).ToDictionary(user => user.Value, StringComparer.Ordinal);
        var roles = await _roleService.GetRoleNamesAsync();

        return Initialize<CalendarSharingViewModel>("CalendarSharing_Edit", model =>
        {
            model.FreeBusyUsers = Principals(calendar, CalendarPrincipalKind.User, CalendarAccessLevel.FreeBusy);
            model.ReadUsers = Principals(calendar, CalendarPrincipalKind.User, CalendarAccessLevel.Read);
            model.WriteUsers = Principals(calendar, CalendarPrincipalKind.User, CalendarAccessLevel.Write);
            model.ManageUsers = Principals(calendar, CalendarPrincipalKind.User, CalendarAccessLevel.Manage);
            model.FreeBusyRoles = Principals(calendar, CalendarPrincipalKind.Role, CalendarAccessLevel.FreeBusy);
            model.ReadRoles = Principals(calendar, CalendarPrincipalKind.Role, CalendarAccessLevel.Read);
            model.WriteRoles = Principals(calendar, CalendarPrincipalKind.Role, CalendarAccessLevel.Write);
            model.ManageRoles = Principals(calendar, CalendarPrincipalKind.Role, CalendarAccessLevel.Manage);

            foreach (var level in new[] { CalendarAccessLevel.FreeBusy, CalendarAccessLevel.Read, CalendarAccessLevel.Write, CalendarAccessLevel.Manage })
            {
                var selected = Principals(calendar, CalendarPrincipalKind.User, level)
                    .Select(id => users.TryGetValue(id, out var user) ? new { value = user.Value, text = user.Text } : new { value = id, text = id });

                model.InitialUsersJson[level.ToString()] = JsonSerializer.Serialize(selected, _jsonOptions);
            }

            model.RoleOptions = roles
                .Where(role => !string.Equals(role, "Anonymous", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(role => new SelectListItem(role, role))
                .ToList();
        }).Location("Content:10%Sharing;3");
    }

    public override async Task<IDisplayResult> UpdateAsync(Calendar calendar, UpdateEditorContext context)
    {
        if (!context.IsNew && await _accessService.GetAccessLevelAsync(_httpContextAccessor.HttpContext?.User, calendar) < CalendarAccessLevel.Manage)
        {
            return null;
        }

        var model = new CalendarSharingViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var shares = new List<CalendarShare>();

        Add(shares, model.FreeBusyUsers, CalendarPrincipalKind.User, CalendarAccessLevel.FreeBusy);
        Add(shares, model.ReadUsers, CalendarPrincipalKind.User, CalendarAccessLevel.Read);
        Add(shares, model.WriteUsers, CalendarPrincipalKind.User, CalendarAccessLevel.Write);
        Add(shares, model.ManageUsers, CalendarPrincipalKind.User, CalendarAccessLevel.Manage);
        Add(shares, model.FreeBusyRoles, CalendarPrincipalKind.Role, CalendarAccessLevel.FreeBusy);
        Add(shares, model.ReadRoles, CalendarPrincipalKind.Role, CalendarAccessLevel.Read);
        Add(shares, model.WriteRoles, CalendarPrincipalKind.Role, CalendarAccessLevel.Write);
        Add(shares, model.ManageRoles, CalendarPrincipalKind.Role, CalendarAccessLevel.Manage);

        calendar.Shares = shares;

        return await EditAsync(calendar, context);
    }

    private static string[] Principals(Calendar calendar, CalendarPrincipalKind kind, CalendarAccessLevel level)
    {
        return calendar.Shares
            .Where(share => share.PrincipalKind == kind && share.Access == level)
            .Select(share => share.PrincipalId)
            .ToArray();
    }

    private static void Add(List<CalendarShare> shares, IEnumerable<string> principals, CalendarPrincipalKind kind, CalendarAccessLevel level)
    {
        foreach (var principal in principals ?? [])
        {
            if (!string.IsNullOrWhiteSpace(principal))
            {
                shares.Add(new CalendarShare
                {
                    PrincipalKind = kind,
                    PrincipalId = principal.Trim(),
                    Access = level,
                });
            }
        }
    }
}
