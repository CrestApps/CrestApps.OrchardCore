using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// The Workforce card on Pay Setup's pay code editor: the hours a shift differential applies in, a time rule kept in
/// the code's <see cref="WorkforcePayCodeSettings"/>.
/// </summary>
internal sealed class WorkforcePayCodeDisplayDriver : DisplayDriver<PayCode>
{
    public override IDisplayResult Edit(PayCode code, BuildEditorContext context)
    {
        return Initialize<WorkforcePayCodeWindowViewModel>("WorkforcePayCodeWindow_Edit", model =>
        {
            var window = code.GetWorkforceSettings();

            model.WindowStart = window.WindowStart;
            model.WindowEnd = window.WindowEnd;
            model.Weekdays = window.Weekdays.Select(day => day.ToString()).ToList();
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(PayCode code, UpdateEditorContext context)
    {
        var model = new WorkforcePayCodeWindowViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var weekdays = (model.Weekdays ?? [])
            .Select(day => Enum.TryParse<DayOfWeek>(day, out var parsed) ? parsed : (DayOfWeek?)null)
            .Where(day => day.HasValue)
            .Select(day => day.Value)
            .Distinct()
            .ToList();

        // Only a shift differential reads the window; any other code keeps none.
        if (code.Kind == PayCodeKinds.ShiftDifferential || model.WindowStart.HasValue || model.WindowEnd.HasValue)
        {
            code.Put(new WorkforcePayCodeSettings
            {
                WindowStart = model.WindowStart,
                WindowEnd = model.WindowEnd,
                Weekdays = weekdays,
            });
        }
        else
        {
            code.Properties.Remove(nameof(WorkforcePayCodeSettings));
        }

        return Edit(code, context);
    }
}
