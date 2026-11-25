using CrestApps.OrchardCore.Approvals.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Approvals.Drivers;

/// <summary>
/// The editor of an approver's decision: the note, in its own card. The posted field keeps its name, so the page's
/// action binds it as before.
/// </summary>
internal sealed class ApprovalDecideDisplayDriver : DisplayDriver<ApprovalDecideViewModel>
{
    internal readonly IStringLocalizer S;

    public ApprovalDecideDisplayDriver(IStringLocalizer<ApprovalDecideDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ApprovalDecideViewModel model, BuildEditorContext context)
        => Initialize<ApprovalDecideViewModel>("ApprovalDecide_Edit", m => m.Note = model.Note)
            .Location(l => l.Zone("Content", "1").Card(S["Your Decision"], "10"));

    public override async Task<IDisplayResult> UpdateAsync(ApprovalDecideViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Note);

        return Edit(model, context);
    }

    // The note keeps the name the page posts.
    protected override void BuildPrefix(ApprovalDecideViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }
}
