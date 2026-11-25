using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Drivers;

/// <summary>
/// Builds the editor of an adherence exception, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class AdherenceExceptionEditDisplayDriver : DisplayDriver<AdherenceExceptionEditViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdherenceExceptionEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdherenceExceptionEditDisplayDriver(IStringLocalizer<AdherenceExceptionEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(AdherenceExceptionEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<AdherenceExceptionEditViewModel>("AdherenceExceptionDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Details"], "10")));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(AdherenceExceptionEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.WorkerId, m => m.Date, m => m.Start, m => m.End, m => m.Reason, m => m.Note);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(AdherenceExceptionEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(AdherenceExceptionEditViewModel source, AdherenceExceptionEditViewModel model)
    {
        model.WorkerId = source.WorkerId;
        model.Date = source.Date;
        model.Start = source.Start;
        model.End = source.End;
        model.Reason = source.Reason;
        model.Note = source.Note;
        model.ReturnUrl = source.ReturnUrl;
        model.WorkerOptions = source.WorkerOptions;
    }
}
