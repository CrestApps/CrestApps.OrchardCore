using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Edits the stage, probability and campaign of an opportunity. The amount, close date, owner, primary contact and
/// lead source are content fields of the part, edited by their own field editors.
/// </summary>
internal sealed class OpportunityPartDisplayDriver : ContentPartDisplayDriver<OpportunityPart>
{
    private readonly INamedCatalog<OpportunityStage> _stages;
    private readonly ICatalogManager<OmnichannelCampaign> _campaigns;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpportunityPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="stages">The opportunity stage catalog.</param>
    /// <param name="campaigns">The campaign manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OpportunityPartDisplayDriver(
        INamedCatalog<OpportunityStage> stages,
        ICatalogManager<OmnichannelCampaign> campaigns,
        IStringLocalizer<OpportunityPartDisplayDriver> stringLocalizer)
    {
        _stages = stages;
        _campaigns = campaigns;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(OpportunityPart part, BuildPartDisplayContext context)
    {
        return Initialize<OpportunityPartViewModel>("OpportunityPart_SummaryAdmin", async model =>
        {
            var stage = string.IsNullOrEmpty(part.StageId)
                ? null
                : (await _stages.GetAllAsync()).FirstOrDefault(entry => entry.ItemId == part.StageId);

            model.StageName = stage?.Name;
            model.IsClosed = part.IsClosed;
            model.IsWon = part.IsWon;
            model.Amount = part.Amount?.Value;
            model.CloseDate = part.CloseDate?.Value;
            model.Probability = part.Probability;
        }).Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:4");
    }

    public override IDisplayResult Edit(OpportunityPart part, BuildPartEditorContext context)
    {
        return Initialize<OpportunityPartViewModel>(GetEditorShapeType(context), async model =>
        {
            var stages = OpportunityStages.ForType(await _stages.GetAllAsync(), context.TypePartDefinition.ContentTypeDefinition);

            model.StageId = part.StageId;
            model.Probability = part.Probability;
            model.CampaignId = part.CampaignId;
            model.Stages = stages
                .Select(stage => new SelectListItem($"{stage.Name} ({stage.Probability}%)", stage.ItemId, stage.ItemId == part.StageId))
                .ToList();
            model.Campaigns = (await _campaigns.GetAllAsync())
                .OrderBy(campaign => campaign.DisplayText, StringComparer.OrdinalIgnoreCase)
                .Select(campaign => new SelectListItem(campaign.DisplayText, campaign.ItemId, campaign.ItemId == part.CampaignId))
                .ToList();
        }).Location("Parts:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(OpportunityPart part, UpdatePartEditorContext context)
    {
        var model = new OpportunityPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var stages = OpportunityStages.ForType(await _stages.GetAllAsync(), context.TypePartDefinition.ContentTypeDefinition);
        var stage = stages.FirstOrDefault(entry => entry.ItemId == model.StageId);

        if (!string.IsNullOrEmpty(model.StageId) && stage is null)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.StageId), S["Choose one of the stages of this opportunity type."]);
        }

        if (model.Probability is < 0 or > 100)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Probability), S["The probability must be between 0 and 100."]);
        }

        // A stage change resets the probability to the new stage's default unless the editor typed its own.
        var stageChanged = !string.Equals(part.StageId, model.StageId, StringComparison.Ordinal);

        part.StageId = string.IsNullOrEmpty(model.StageId) ? null : model.StageId;
        part.Probability = stageChanged && model.Probability == part.Probability
            ? stage?.Probability
            : model.Probability;
        part.CampaignId = string.IsNullOrWhiteSpace(model.CampaignId) ? null : model.CampaignId.Trim();

        return Edit(part, context);
    }
}
