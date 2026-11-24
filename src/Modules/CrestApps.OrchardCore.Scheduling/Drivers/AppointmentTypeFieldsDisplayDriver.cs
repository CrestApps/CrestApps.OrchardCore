using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Scheduling.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Scheduling.Drivers;

/// <summary>
/// The built-in sections of the appointment type editor: general details, time, the resource groups that take the
/// appointment, and the booking rules. Other modules add their own sections to the editor with drivers of their own for
/// <see cref="AppointmentType"/>.
/// </summary>
internal sealed class AppointmentTypeFieldsDisplayDriver : DisplayDriver<AppointmentType>
{
    private readonly AppointmentTypeStore _typeStore;
    private readonly SchedulingResourceStore _resourceStore;

    internal readonly IStringLocalizer S;

    public AppointmentTypeFieldsDisplayDriver(
        AppointmentTypeStore typeStore,
        SchedulingResourceStore resourceStore,
        IStringLocalizer<AppointmentTypeFieldsDisplayDriver> stringLocalizer)
    {
        _typeStore = typeStore;
        _resourceStore = resourceStore;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AppointmentType type, BuildEditorContext context)
    {
        return Initialize<AppointmentTypeViewModel>("AppointmentTypeFields_Edit", async model =>
        {
            AppointmentTypeEditorRules.Fill(model, type);

            await FillAsync(model);
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AppointmentType type, UpdateEditorContext context)
    {
        var posted = new AppointmentTypeViewModel();

        await context.Updater.TryUpdateModelAsync(posted, Prefix);

        foreach (var error in await AppointmentTypeEditorRules.ApplyAsync(type, posted, _typeStore, S))
        {
            context.Updater.ModelState.AddModelError(Key(error.Key), error.Value);
        }

        // The form shows what was posted, as typed.
        return Initialize<AppointmentTypeViewModel>("AppointmentTypeFields_Edit", async model =>
        {
            model.Name = posted.Name;
            model.Description = posted.Description;
            model.Slug = posted.Slug;
            model.DurationMinutes = posted.DurationMinutes;
            model.SlotIntervalMinutes = posted.SlotIntervalMinutes;
            model.BufferBeforeMinutes = posted.BufferBeforeMinutes;
            model.BufferAfterMinutes = posted.BufferAfterMinutes;
            model.MinimumNoticeMinutes = posted.MinimumNoticeMinutes;
            model.HorizonDays = posted.HorizonDays;
            model.MaxPerDay = posted.MaxPerDay;
            model.MaxPerWeek = posted.MaxPerWeek;
            model.MaxUpcomingPerCustomer = posted.MaxUpcomingPerCustomer;
            model.CancelNoticeMinutes = posted.CancelNoticeMinutes;
            model.RescheduleNoticeMinutes = posted.RescheduleNoticeMinutes;
            model.Capacity = posted.Capacity;
            model.Requirements = posted.Requirements;
            model.RequiresConfirmation = posted.RequiresConfirmation;
            model.HoldMinutes = posted.HoldMinutes;
            model.AllowSelfService = posted.AllowSelfService;
            model.Location = posted.Location;
            model.Color = posted.Color;
            model.Enabled = posted.Enabled;

            await FillAsync(model);
        }).Location("Content:1");
    }

    private string Key(string name)
        => string.IsNullOrEmpty(Prefix) ? name : $"{Prefix}.{name}";

    private async Task FillAsync(AppointmentTypeViewModel model)
    {
        // Show the groups in use, then a few empty ones the page reveals one at a time.
        model.Requirements = (model.Requirements ?? []).Where(group => group?.ResourceIds?.Any(id => !string.IsNullOrEmpty(id)) == true).ToList();
        model.VisibleRequirements = Math.Max(1, model.Requirements.Count);

        while (model.Requirements.Count < model.VisibleRequirements + 3)
        {
            model.Requirements.Add(new AppointmentResourceRequirementViewModel { Assignment = nameof(AppointmentAssignment.AnyAvailable) });
        }

        model.ResourceOptions = (await _resourceStore.ListAsync())
            .Select(resource => new SelectListItem(resource.Name, resource.ItemId))
            .ToList();
        model.AssignmentOptions =
        [
            new SelectListItem(S["Any free resource"], nameof(AppointmentAssignment.AnyAvailable)),
            new SelectListItem(S["Share out (round robin)"], nameof(AppointmentAssignment.RoundRobin)),
            new SelectListItem(S["All resources together"], nameof(AppointmentAssignment.AllTogether)),
        ];
    }

}
