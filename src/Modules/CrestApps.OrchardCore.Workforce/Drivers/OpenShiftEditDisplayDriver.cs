using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Builds the editor of a open shift, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class OpenShiftEditDisplayDriver : DisplayDriver<OpenShiftEditViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenShiftEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OpenShiftEditDisplayDriver(IStringLocalizer<OpenShiftEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(OpenShiftEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<OpenShiftEditViewModel>("OpenShiftDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Where And When"], "10")),
            Initialize<OpenShiftEditViewModel>("OpenShiftShift_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Shift"], "20")));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(OpenShiftEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.TeamId, m => m.Date, m => m.TemplateId, m => m.Start, m => m.End, m => m.Segments, m => m.Notes);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(OpenShiftEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(OpenShiftEditViewModel source, OpenShiftEditViewModel model)
    {
        model.TeamId = source.TeamId;
        model.Date = source.Date;
        model.TemplateId = source.TemplateId;
        model.Start = source.Start;
        model.End = source.End;
        model.Segments = source.Segments;
        model.Notes = source.Notes;
        model.ActivityNames = source.ActivityNames;
        model.TeamOptions = source.TeamOptions;
        model.TemplateOptions = source.TemplateOptions;
    }
}
