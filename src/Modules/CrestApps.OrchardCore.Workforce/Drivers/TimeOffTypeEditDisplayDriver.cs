using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Builds the editor of a time-off type, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class TimeOffTypeEditDisplayDriver : DisplayDriver<TimeOffTypeEditViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffTypeEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeOffTypeEditDisplayDriver(IStringLocalizer<TimeOffTypeEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(TimeOffTypeEditViewModel model, BuildEditorContext context)
    {
        var results = new List<IDisplayResult>
        {
            Initialize<TimeOffTypeEditViewModel>("TimeOffTypeDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Details"], "10")),
            Initialize<TimeOffTypeEditViewModel>("TimeOffTypeCounting_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Counting"], "20")),
            Initialize<TimeOffTypeEditViewModel>("TimeOffTypePay_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Pay"], "30")),
            Initialize<TimeOffTypeEditViewModel>("TimeOffTypeRules_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "4").Card(S["Request Rules"], "40")),
        };

        if (model.StageOptions is not null)
        {
            results.Add(Initialize<TimeOffTypeEditViewModel>("TimeOffTypeTimeTracker_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "5").Card(S["Time Tracker"], "50")));
        }

        return Combine([.. results]);
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(TimeOffTypeEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.Description, m => m.Color, m => m.Enabled, m => m.Unit, m => m.HoursPerDay, m => m.PayCode, m => m.IsPaid, m => m.RequiresBalance, m => m.AllowPartialDay, m => m.RequiresReason, m => m.CountsAgainstAllotment, m => m.VisibleToWorkers, m => m.StageId);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(TimeOffTypeEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(TimeOffTypeEditViewModel source, TimeOffTypeEditViewModel model)
    {
        model.Id = source.Id;
        model.Name = source.Name;
        model.Description = source.Description;
        model.Color = source.Color;
        model.Unit = source.Unit;
        model.HoursPerDay = source.HoursPerDay;
        model.IsPaid = source.IsPaid;
        model.RequiresBalance = source.RequiresBalance;
        model.AllowPartialDay = source.AllowPartialDay;
        model.RequiresReason = source.RequiresReason;
        model.CountsAgainstAllotment = source.CountsAgainstAllotment;
        model.PayCode = source.PayCode;
        model.VisibleToWorkers = source.VisibleToWorkers;
        model.StageId = source.StageId;
        model.StageOptions = source.StageOptions;
        model.Enabled = source.Enabled;
    }
}
