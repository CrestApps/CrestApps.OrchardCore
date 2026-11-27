using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Builds the editor of a time off request, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class TimeOffRequestEditDisplayDriver : DisplayDriver<TimeOffRequestEditViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffRequestEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeOffRequestEditDisplayDriver(IStringLocalizer<TimeOffRequestEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(TimeOffRequestEditViewModel model, BuildEditorContext context)
    {
        var results = new List<IDisplayResult>
        {
            Initialize<TimeOffRequestEditViewModel>("TimeOffRequestDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Request"], "10")),
        };

        if (model.Preview is not null)
        {
            results.Add(Initialize<TimeOffRequestEditViewModel>("TimeOffRequestPreview_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["What This Request Would Do"], "20")));
        }

        return Combine([.. results]);
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(TimeOffRequestEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.WorkerId, m => m.TypeId, m => m.StartDate, m => m.EndDate, m => m.PartialDay, m => m.StartTime, m => m.EndTime, m => m.Reason);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(TimeOffRequestEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(TimeOffRequestEditViewModel source, TimeOffRequestEditViewModel model)
    {
        model.WorkerId = source.WorkerId;
        model.TypeId = source.TypeId;
        model.StartDate = source.StartDate;
        model.EndDate = source.EndDate;
        model.PartialDay = source.PartialDay;
        model.StartTime = source.StartTime;
        model.EndTime = source.EndTime;
        model.Reason = source.Reason;
        model.Preview = source.Preview;
        model.WorkerName = source.WorkerName;
        model.WorkerOptions = source.WorkerOptions;
        model.TypeOptions = source.TypeOptions;
    }
}
