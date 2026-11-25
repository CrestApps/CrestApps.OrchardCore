using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Builds the editor of a shift trade, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class ShiftTradeEditDisplayDriver : DisplayDriver<ShiftTradeCreateViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShiftTradeEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ShiftTradeEditDisplayDriver(IStringLocalizer<ShiftTradeEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(ShiftTradeCreateViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<ShiftTradeCreateViewModel>("ShiftTradeDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Trade"], "10")));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(ShiftTradeCreateViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.WorkerId, m => m.Kind, m => m.ShiftEventId, m => m.TargetWorkerId, m => m.TargetShiftEventId, m => m.Note);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(ShiftTradeCreateViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(ShiftTradeCreateViewModel source, ShiftTradeCreateViewModel model)
    {
        model.WorkerId = source.WorkerId;
        model.Kind = source.Kind;
        model.ShiftEventId = source.ShiftEventId;
        model.TargetWorkerId = source.TargetWorkerId;
        model.TargetShiftEventId = source.TargetShiftEventId;
        model.Note = source.Note;
        model.WorkerOptions = source.WorkerOptions;
        model.ShiftOptions = source.ShiftOptions;
        model.TargetOptions = source.TargetOptions;
        model.TargetShiftOptions = source.TargetShiftOptions;
    }
}
