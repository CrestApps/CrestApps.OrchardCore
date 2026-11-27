using CrestApps.OrchardCore.Marketing.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Marketing.Drivers;

/// <summary>
/// Builds the editor of a segment, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class SegmentEditDisplayDriver : DisplayDriver<SegmentEditViewModel>
{
    internal readonly IStringLocalizer S;

    public SegmentEditDisplayDriver(IStringLocalizer<SegmentEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(SegmentEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<SegmentEditViewModel>("SegmentGeneral_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["General"], "10")),
            Initialize<SegmentEditViewModel>("SegmentRules_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Customers Who"], "20")));
    }

    public override async Task<IDisplayResult> UpdateAsync(SegmentEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.Name,
            m => m.Description,
            m => m.MinOrders,
            m => m.MaxOrders,
            m => m.MinSpent,
            m => m.MaxSpent,
            m => m.LastOrderAtLeastDaysAgo,
            m => m.LastOrderWithinDays,
            m => m.FirstOrderWithinDays,
            m => m.Country);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates.
    protected override void BuildPrefix(SegmentEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(SegmentEditViewModel source, SegmentEditViewModel model)
    {
        model.ItemId = source.ItemId;
        model.StoreCurrency = source.StoreCurrency;
        model.Name = source.Name;
        model.Description = source.Description;
        model.MinOrders = source.MinOrders;
        model.MaxOrders = source.MaxOrders;
        model.MinSpent = source.MinSpent;
        model.MaxSpent = source.MaxSpent;
        model.LastOrderAtLeastDaysAgo = source.LastOrderAtLeastDaysAgo;
        model.LastOrderWithinDays = source.LastOrderWithinDays;
        model.FirstOrderWithinDays = source.FirstOrderWithinDays;
        model.Country = source.Country;
    }
}
