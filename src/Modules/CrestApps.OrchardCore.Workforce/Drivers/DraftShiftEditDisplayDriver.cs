using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Builds the editor of a draft shift, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class DraftShiftEditDisplayDriver : DisplayDriver<DraftShiftEditViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DraftShiftEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DraftShiftEditDisplayDriver(IStringLocalizer<DraftShiftEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(DraftShiftEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<DraftShiftEditViewModel>("DraftShiftDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Who And When"], "10")),
            Initialize<DraftShiftEditViewModel>("DraftShiftShift_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Shift"], "20")));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(DraftShiftEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.WorkerId, m => m.Date, m => m.TemplateId, m => m.Start, m => m.End, m => m.Segments, m => m.Notes);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(DraftShiftEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(DraftShiftEditViewModel source, DraftShiftEditViewModel model)
    {
        model.Id = source.Id;
        model.PeriodId = source.PeriodId;
        model.WorkerId = source.WorkerId;
        model.Date = source.Date;
        model.TemplateId = source.TemplateId;
        model.Start = source.Start;
        model.End = source.End;
        model.Segments = source.Segments;
        model.Notes = source.Notes;
        model.PeriodName = source.PeriodName;
        model.ActivityNames = source.ActivityNames;
        model.WorkerOptions = source.WorkerOptions;
        model.TemplateOptions = source.TemplateOptions;
        model.PublishedEventId = source.PublishedEventId;
        model.PublishedWorkerName = source.PublishedWorkerName;
        model.PublishedStart = source.PublishedStart;
        model.PublishedEnd = source.PublishedEnd;
    }
}
