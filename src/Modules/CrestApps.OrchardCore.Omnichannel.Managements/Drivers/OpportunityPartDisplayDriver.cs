using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Lists.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OpportunityPartDisplayDriver : ContentPartDisplayDriver<OpportunityPart>
{
    private readonly INamedCatalog<OpportunityStage> _stages;
    private readonly ICatalogManager<OmnichannelCampaign> _campaigns;
    private readonly LeadSourceProvider _sources;
    private readonly IContentManager _contentManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpportunityPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="stages">The opportunity stage catalog.</param>
    /// <param name="campaigns">The campaign manager.</param>
    /// <param name="sources">The lead sources.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OpportunityPartDisplayDriver(
        INamedCatalog<OpportunityStage> stages,
        ICatalogManager<OmnichannelCampaign> campaigns,
        LeadSourceProvider sources,
        IContentManager contentManager,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<OpportunityPartDisplayDriver> stringLocalizer)
    {
        _stages = stages;
        _campaigns = campaigns;
        _sources = sources;
        _contentManager = contentManager;
        _httpContextAccessor = httpContextAccessor;
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
            model.Amount = part.Amount;
            model.CloseDate = part.CloseDate;
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
            model.Amount = part.Amount;
            model.CloseDate = part.CloseDate;
            model.OwnerId = part.OwnerId;
            model.SourceId = part.SourceId;
            model.CampaignId = part.CampaignId;
            model.PrimaryContactItemId = part.PrimaryContactItemId;
            model.AccountContentItemId = GetAccountId(part.ContentItem);
            model.Stages = stages
                .Select(stage => new SelectListItem($"{stage.Name} ({stage.Probability}%)", stage.ItemId, stage.ItemId == part.StageId))
                .ToList();
            model.Campaigns = (await _campaigns.GetAllAsync())
                .OrderBy(campaign => campaign.DisplayText, StringComparer.OrdinalIgnoreCase)
                .Select(campaign => new SelectListItem(campaign.DisplayText, campaign.ItemId, campaign.ItemId == part.CampaignId))
                .ToList();
            model.Sources = await _sources.GetOptionsAsync(part.SourceId);

            if (!string.IsNullOrEmpty(part.PrimaryContactItemId))
            {
                var contact = await _contentManager.GetAsync(part.PrimaryContactItemId, VersionOptions.Latest);

                model.PrimaryContactDisplayText = contact?.DisplayText ?? part.PrimaryContactItemId;
            }
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

        if (model.Amount is < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Amount), S["The amount cannot be negative."]);
        }

        // A stage change resets the probability to the new stage's default unless the editor typed its own.
        var stageChanged = !string.Equals(part.StageId, model.StageId, StringComparison.Ordinal);

        part.StageId = string.IsNullOrEmpty(model.StageId) ? null : model.StageId;
        part.Probability = stageChanged && model.Probability == part.Probability
            ? stage?.Probability
            : model.Probability;
        part.Amount = model.Amount;
        part.CloseDate = model.CloseDate?.Date;
        part.OwnerId = Trim(model.OwnerId);
        part.SourceId = await _sources.FindIdAsync(model.SourceId);
        part.CampaignId = Trim(model.CampaignId);
        part.PrimaryContactItemId = Trim(model.PrimaryContactItemId);

        return Edit(part, context);
    }

    private string GetAccountId(ContentItem contentItem)
    {
        if (contentItem?.TryGet<ContainedPart>(out var contained) == true && !string.IsNullOrEmpty(contained.ListContentItemId))
        {
            return contained.ListContentItemId;
        }

        // A new opportunity created from inside an account carries the account in the query string until it is saved.
        return _httpContextAccessor.HttpContext?.Request.Query["ListPart.ContainerId"].ToString() is { Length: > 0 } containerId
            ? containerId
            : null;
    }

    private static string Trim(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
