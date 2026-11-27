using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Reports.Designer.Drivers;

/// <summary>
/// Builds the editor of a report group in cards.
/// </summary>
internal sealed class ReportGroupEditDisplayDriver : DisplayDriver<ReportGroupEditViewModel>
{
    internal readonly IStringLocalizer S;

    public ReportGroupEditDisplayDriver(IStringLocalizer<ReportGroupEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ReportGroupEditViewModel model, BuildEditorContext context)
    {
        return Initialize<ReportGroupEditViewModel>("ReportGroupFields_Edit", m =>
        {
            m.IsNew = model.IsNew;
            m.DisplayText = model.DisplayText;
            m.ParentId = model.ParentId;
            m.Order = model.Order;
            m.Parents = model.Parents;
        }).Location(l => l.Zone("Content", "1").Card(S["General"], "10"));
    }

    public override async Task<IDisplayResult> UpdateAsync(ReportGroupEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.DisplayText, m => m.ParentId, m => m.Order);

        return Edit(model, context);
    }

    // The fields keep the names the page posts.
    protected override void BuildPrefix(ReportGroupEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }
}