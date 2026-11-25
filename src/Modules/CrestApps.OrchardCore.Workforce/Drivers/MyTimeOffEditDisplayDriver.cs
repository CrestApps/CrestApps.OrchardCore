using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Builds the form of a worker's time off request in the My Work portal, grouped in cards by their placement.
/// </summary>
internal sealed class MyTimeOffEditDisplayDriver : DisplayDriver<MyTimeOffViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyTimeOffEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MyTimeOffEditDisplayDriver(IStringLocalizer<MyTimeOffEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(MyTimeOffViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<MyTimeOffViewModel>("MyTimeOffRequest_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Ask For Time Off"], "10")),
            Initialize<MyTimeOffViewModel>("MyTimeOffReason_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Details"], "20")));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(MyTimeOffViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.TypeId, m => m.StartDate, m => m.EndDate, m => m.PartialDay, m => m.StartTime, m => m.EndTime, m => m.Reason);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the script of the page selects.
    protected override void BuildPrefix(MyTimeOffViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(MyTimeOffViewModel source, MyTimeOffViewModel model)
    {
        model.TypeOptions = source.TypeOptions;
        model.TypeId = source.TypeId;
        model.StartDate = source.StartDate;
        model.EndDate = source.EndDate;
        model.PartialDay = source.PartialDay;
        model.StartTime = source.StartTime;
        model.EndTime = source.EndTime;
        model.Reason = source.Reason;
    }
}
