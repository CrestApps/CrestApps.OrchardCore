using CrestApps.OrchardCore.TimeTracker.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.TimeTracker.Drivers;

/// <summary>
/// The cards of the form that adds or changes a time entry.
/// </summary>
internal sealed class TimeEntryEditDisplayDriver : DisplayDriver<TimeEntryEditViewModel>
{
    internal readonly IStringLocalizer S;

    public TimeEntryEditDisplayDriver(IStringLocalizer<TimeEntryEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TimeEntryEditViewModel model, BuildEditorContext context)
    {
        var results = new List<IDisplayResult>
        {
            Initialize<TimeEntryEditViewModel>("TimeEntryPerson_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Person And Stage"], "10")),
            Initialize<TimeEntryEditViewModel>("TimeEntryWhen_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["When"], "20")),
        };

        // Time that another clock keeps is corrected there, so nothing here says why it changes.
        if (model.ExternalClock is null)
        {
            results.Add(Initialize<TimeEntryEditViewModel>("TimeEntryReason_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Reason"], "30")));
        }

        return Combine([.. results]);
    }

    public override async Task<IDisplayResult> UpdateAsync(TimeEntryEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.UserId, m => m.StageId, m => m.Start, m => m.End, m => m.Note, m => m.Reason);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(TimeEntryEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(TimeEntryEditViewModel source, TimeEntryEditViewModel model)
    {
        model.Id = source.Id;
        model.UserId = source.UserId;
        model.UserName = source.UserName;
        model.StageId = source.StageId;
        model.Start = source.Start;
        model.End = source.End;
        model.Note = source.Note;
        model.Reason = source.Reason;
        model.IsOpen = source.IsOpen;
        model.ExternalClock = source.ExternalClock;
        model.Changes = source.Changes;
        model.StageOptions = source.StageOptions;
        model.TimeZoneId = source.TimeZoneId;
    }
}
