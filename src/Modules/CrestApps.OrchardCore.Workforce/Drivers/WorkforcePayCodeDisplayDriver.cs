using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// The Workforce card on Pay Setup's pay code editor: the hours a shift differential applies in, a time rule kept in
/// the code's <see cref="WorkforcePayCodeSettings"/>.
/// </summary>
internal sealed class WorkforcePayCodeDisplayDriver : DisplayDriver<PayCode>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforcePayCodeDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforcePayCodeDisplayDriver(IStringLocalizer<WorkforcePayCodeDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(PayCode code, BuildEditorContext context)
    {
        return Initialize<WorkforcePayCodeWindowViewModel>("WorkforcePayCodeWindow_Edit", model =>
        {
            var window = code.GetWorkforceSettings();

            model.WindowStart = window.WindowStart;
            model.WindowEnd = window.WindowEnd;
            model.Weekdays = window.Weekdays.Select(day => day.ToString()).ToList();
        }).Location(l => l.Zone("Content", "5").Card(S["Shift Differential Hours"], "70"));
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
