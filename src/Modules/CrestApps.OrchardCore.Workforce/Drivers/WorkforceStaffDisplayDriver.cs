using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Workforce's card on the staff editor: the fewest and most hours to schedule, and when the person can work.
/// </summary>
internal sealed class WorkforceStaffDisplayDriver : DisplayDriver<StaffMember>
{
    internal readonly IStringLocalizer S;

    public WorkforceStaffDisplayDriver(IStringLocalizer<WorkforceStaffDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(StaffMember staff, BuildEditorContext context)
        => Editor(new WorkforceStaffEditViewModel
        {
            MinHoursPerWeek = staff.MinHoursPerWeek,
            MaxHoursPerWeek = staff.MaxHoursPerWeek,
            MaxHoursPerDay = staff.MaxHoursPerDay,
            Availability = AvailabilityFormat.Write(staff.Availability),
        });

    public override async Task<IDisplayResult> UpdateAsync(StaffMember staff, UpdateEditorContext context)
    {
        var model = new WorkforceStaffEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var errors = new List<string>();
        var availability = AvailabilityFormat.Read(model.Availability, errors);

        foreach (var error in errors)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(WorkforceStaffEditViewModel.Availability), S["This line can't be read: {0}", error]);
        }

        staff.MinHoursPerWeek = model.MinHoursPerWeek;
        staff.MaxHoursPerWeek = model.MaxHoursPerWeek;
        staff.MaxHoursPerDay = model.MaxHoursPerDay;

        if (errors.Count == 0)
        {
            staff.Availability = availability;
        }

        // The posted values are shown again as typed, so a line that can't be read stays to be fixed.
        return Editor(model);
    }

    private ShapeResult Editor(WorkforceStaffEditViewModel values)
        => Initialize<WorkforceStaffEditViewModel>("WorkforceStaffFields_Edit", model =>
        {
            model.MinHoursPerWeek = values.MinHoursPerWeek;
            model.MaxHoursPerWeek = values.MaxHoursPerWeek;
            model.MaxHoursPerDay = values.MaxHoursPerDay;
            model.Availability = values.Availability;
        }).Location("Content:5");
}
