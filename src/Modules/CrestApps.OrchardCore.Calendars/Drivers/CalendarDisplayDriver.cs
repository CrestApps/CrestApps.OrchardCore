using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Calendars.Drivers;

/// <summary>
/// The calendar's row in the list of calendars, and the General card of its editor.
/// </summary>
internal sealed class CalendarDisplayDriver : DisplayDriver<Calendar>
{
    private readonly ICalendarTypeManager _typeManager;
    private readonly ICalendarAccessService _accessService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public CalendarDisplayDriver(
        ICalendarTypeManager typeManager,
        ICalendarAccessService accessService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizerFactory localizerFactory,
        IClock clock,
        IStringLocalizer<CalendarDisplayDriver> stringLocalizer)
    {
        _typeManager = typeManager;
        _accessService = accessService;
        _httpContextAccessor = httpContextAccessor;
        _localizerFactory = localizerFactory;
        _clock = clock;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> DisplayAsync(Calendar calendar, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = calendar.Name?.ToLowerInvariant() ?? string.Empty;
        }

        var type = _typeManager.FindType(calendar.Type);
        var level = await _accessService.GetAccessLevelAsync(_httpContextAccessor.HttpContext?.User, calendar);

        void Populate(CalendarRowViewModel model)
        {
            model.Calendar = calendar;
            model.TypeName = type is null ? calendar.Type : _localizerFactory.Localize(type.DisplayName).Value;
            model.TypeIcon = type?.Icon ?? "fa-solid fa-calendar";
            model.Level = level;
            model.OwnerName = calendar.OwnerKind == CalendarOwnerKind.Role ? calendar.OwnerId : null;
        }

        return Combine(
            Initialize<CalendarRowViewModel>("Calendar_Fields_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            Initialize<CalendarRowViewModel>("Calendar_Description_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrWhiteSpace(calendar.Description))),
            Initialize<CalendarRowViewModel>("Calendar_DefaultTags_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Type:5"),
            Initialize<CalendarRowViewModel>("Calendar_Buttons_SummaryAdmin", Populate).Location("Actions:5"),
            Initialize<CalendarRowViewModel>("Calendar_ActionsMenu_SummaryAdmin", Populate).Location("ActionsMenu:10"));
    }

    public override IDisplayResult Edit(Calendar calendar, BuildEditorContext context)
    {
        var type = _typeManager.FindType(calendar.Type);

        return Initialize<CalendarGeneralViewModel>("CalendarGeneral_Edit", model =>
        {
            model.Name = calendar.Name;
            model.Description = calendar.Description;
            model.Color = string.IsNullOrEmpty(calendar.Color) ? type?.DefaultColor ?? "#1971c2" : calendar.Color;
            model.TimeZoneId = calendar.TimeZoneId;
            model.Visibility = calendar.Visibility.ToString();
            model.Enabled = calendar.Enabled;
            model.TypeName = type is null ? calendar.Type : _localizerFactory.Localize(type.DisplayName).Value;
            model.TypeDescription = type?.Description is null ? null : _localizerFactory.Localize(type.Description).Value;
            model.TypeIcon = type?.Icon ?? "fa-solid fa-calendar";
            model.TimeZoneOptions = CalendarSelectLists.TimeZones(_clock, calendar.TimeZoneId);
            model.VisibilityOptions = CalendarSelectLists.Visibilities(S);
        }).Location("Content:1%General;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(Calendar calendar, UpdateEditorContext context)
    {
        var model = new CalendarGeneralViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        calendar.Name = model.Name?.Trim();
        calendar.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        calendar.Color = string.IsNullOrWhiteSpace(model.Color) ? null : model.Color.Trim();
        calendar.TimeZoneId = string.IsNullOrWhiteSpace(model.TimeZoneId) ? null : CalendarTime.NormalizeZoneId(model.TimeZoneId) ?? model.TimeZoneId;
        calendar.Enabled = model.Enabled;

        if (Enum.TryParse<CalendarVisibility>(model.Visibility, out var visibility))
        {
            calendar.Visibility = visibility;
        }

        if (string.IsNullOrWhiteSpace(calendar.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["The name is required."]);
        }

        return Edit(calendar, context);
    }
}
