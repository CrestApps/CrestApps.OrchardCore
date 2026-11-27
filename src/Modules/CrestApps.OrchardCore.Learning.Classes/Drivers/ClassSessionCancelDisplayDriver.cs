using CrestApps.OrchardCore.Learning.Classes.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Classes.Drivers;

/// <summary>
/// The editor of the cancel page of a session: the reason the learners are told.
/// </summary>
internal sealed class ClassSessionCancelDisplayDriver : DisplayDriver<ClassSessionCancelViewModel>
{
    internal readonly IStringLocalizer S;

    public ClassSessionCancelDisplayDriver(IStringLocalizer<ClassSessionCancelDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ClassSessionCancelViewModel model, BuildEditorContext context)
    {
        return Initialize<ClassSessionCancelViewModel>("ClassSessionCancel_Edit", m => m.Reason = model.Reason)
            .Location(l => l.Zone("Content", "1").Card(S["Cancellation"], "10"));
    }

    public override async Task<IDisplayResult> UpdateAsync(ClassSessionCancelViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Reason);

        return Edit(model, context);
    }

    // The field keeps the name the page posts, which the controller validates.
    protected override void BuildPrefix(ClassSessionCancelViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }
}
