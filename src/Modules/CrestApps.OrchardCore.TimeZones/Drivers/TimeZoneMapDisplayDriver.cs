using CrestApps.OrchardCore.TimeZones.Models;
using CrestApps.OrchardCore.TimeZones.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.TimeZones.Drivers;

internal sealed class TimeZoneMapDisplayDriver : DisplayDriver<TimeZoneMap>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneMapDisplayDriver"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeZoneMapDisplayDriver(
        IClock clock,
        IStringLocalizer<TimeZoneMapDisplayDriver> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(TimeZoneMap model, BuildDisplayContext context)
    {
        return CombineAsync(
            View("TimeZoneMap_Fields_SummaryAdmin", model).Location("Content:1"),
            View("TimeZoneMap_Buttons_SummaryAdmin", model).Location("Actions:5"),
            View("TimeZoneMap_DefaultMeta_SummaryAdmin", model).Location("Meta:5"));
    }

    public override IDisplayResult Edit(TimeZoneMap model, BuildEditorContext context)
    {
        return Initialize<TimeZoneMapViewModel>("TimeZoneMapFields_Edit", viewModel =>
        {
            viewModel.IsNew = context.IsNew;
            viewModel.Name = model.Name;
            viewModel.TimeZoneId = NormalizeTimeZoneId(model.TimeZoneId);
            viewModel.TimeZones = GetTimeZoneOptions(viewModel.TimeZoneId);
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(TimeZoneMap model, UpdateEditorContext context)
    {
        var viewModel = new TimeZoneMapViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        if (string.IsNullOrWhiteSpace(viewModel.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.Name), S["Name is a required field."]);
        }

        if (string.IsNullOrWhiteSpace(viewModel.TimeZoneId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.TimeZoneId), S["Time zone is a required field."]);
        }

        if (context.IsNew)
        {
            model.Name = viewModel.Name?.Trim();
        }

        model.TimeZoneId = NormalizeTimeZoneId(viewModel.TimeZoneId);

        return Edit(model, context);
    }

    private IEnumerable<SelectListItem> GetTimeZoneOptions(string selectedTimeZoneId)
    {
        var options = new List<SelectListItem>();

        foreach (var timeZone in _clock.GetTimeZones().OrderBy(x => x.TimeZoneId, StringComparer.Ordinal))
        {
            options.Add(new SelectListItem(timeZone.TimeZoneId, timeZone.TimeZoneId)
            {
                Selected = string.Equals(timeZone.TimeZoneId, selectedTimeZoneId, StringComparison.OrdinalIgnoreCase),
            });
        }

        if (!string.IsNullOrEmpty(selectedTimeZoneId) &&
            options.All(x => !string.Equals(x.Value, selectedTimeZoneId, StringComparison.OrdinalIgnoreCase)))
        {
            options.Add(new SelectListItem(selectedTimeZoneId, selectedTimeZoneId)
            {
                Selected = true,
            });
        }

        return options.OrderBy(x => x.Text, StringComparer.Ordinal);
    }

    private static string NormalizeTimeZoneId(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        return NodaTime.DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZoneId.Trim())?.Id ?? timeZoneId.Trim();
    }
}
