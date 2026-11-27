using CrestApps.OrchardCore.Marketing.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Marketing.Drivers;

/// <summary>
/// Builds the editor of a campaign, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class CampaignEditDisplayDriver : DisplayDriver<CampaignEditViewModel>
{
    internal readonly IStringLocalizer S;

    public CampaignEditDisplayDriver(IStringLocalizer<CampaignEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(CampaignEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<CampaignEditViewModel>("CampaignGeneral_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["General"], "10")),
            Initialize<CampaignEditViewModel>("CampaignEmail_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Email"], "20")),
            Initialize<CampaignEditViewModel>("CampaignDelivery_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Delivery"], "30")));
    }

    public override async Task<IDisplayResult> UpdateAsync(CampaignEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.Name,
            m => m.SegmentId,
            m => m.Subject,
            m => m.Body,
            m => m.ScheduledLocal,
            m => m.TestEmail);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates.
    protected override void BuildPrefix(CampaignEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(CampaignEditViewModel source, CampaignEditViewModel model)
    {
        model.ItemId = source.ItemId;
        model.Campaign = source.Campaign;
        model.Segments = source.Segments;
        model.CanEmail = source.CanEmail;
        model.HasBaseUrl = source.HasBaseUrl;
        model.Name = source.Name;
        model.SegmentId = source.SegmentId;
        model.Subject = source.Subject;
        model.Body = source.Body;
        model.ScheduledLocal = source.ScheduledLocal;
        model.TestEmail = source.TestEmail;
    }
}
