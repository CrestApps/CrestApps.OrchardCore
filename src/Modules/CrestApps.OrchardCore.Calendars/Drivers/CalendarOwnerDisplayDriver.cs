using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Security.Services;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Calendars.Drivers;

/// <summary>
/// The Owner card of the calendar editor, for calendars that belong to a user or a team. Only calendar administrators choose
/// a user owner; a user creating a personal calendar owns it.
/// </summary>
internal sealed class CalendarOwnerDisplayDriver : DisplayDriver<Calendar>
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRoleService _roleService;
    private readonly CalendarUserLookup _userLookup;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public CalendarOwnerDisplayDriver(
        IRoleService roleService,
        CalendarUserLookup userLookup,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<CalendarOwnerDisplayDriver> stringLocalizer)
    {
        _roleService = roleService;
        _userLookup = userLookup;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(Calendar calendar, BuildEditorContext context)
    {
        if (calendar.OwnerKind is not (CalendarOwnerKind.User or CalendarOwnerKind.Role))
        {
            return null;
        }

        if (calendar.OwnerKind == CalendarOwnerKind.User && !await IsAdministratorAsync())
        {
            return null;
        }

        var users = calendar.OwnerKind == CalendarOwnerKind.User ? await _userLookup.GetAsync([calendar.OwnerId]) : [];
        var roles = await _roleService.GetRoleNamesAsync();

        return Initialize<CalendarOwnerViewModel>("CalendarOwner_Edit", model =>
        {
            model.OwnerId = calendar.OwnerId;
            model.OwnerKind = calendar.OwnerKind;
            model.InitialUserJson = JsonSerializer.Serialize(users.Select(user => new { value = user.Value, text = user.Text }), _jsonOptions);
            model.RoleOptions = roles
                .Where(role => !string.Equals(role, "Anonymous", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(role => new SelectListItem(role, role, string.Equals(role, calendar.OwnerId, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }).Location("Content:2%Owner;2");
    }

    public override async Task<IDisplayResult> UpdateAsync(Calendar calendar, UpdateEditorContext context)
    {
        if (calendar.OwnerKind is not (CalendarOwnerKind.User or CalendarOwnerKind.Role))
        {
            return null;
        }

        if (calendar.OwnerKind == CalendarOwnerKind.User && !await IsAdministratorAsync())
        {
            return null;
        }

        var model = new CalendarOwnerViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        calendar.OwnerId = string.IsNullOrWhiteSpace(model.OwnerId) ? null : model.OwnerId.Trim();

        if (string.IsNullOrEmpty(calendar.OwnerId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.OwnerId), calendar.OwnerKind == CalendarOwnerKind.User
                ? S["Choose the user the calendar belongs to."]
                : S["Choose the team (role) the calendar belongs to."]);
        }

        return await EditAsync(calendar, context);
    }

    private Task<bool> IsAdministratorAsync()
    {
        return _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, CalendarPermissions.ManageAllCalendars);
    }
}
