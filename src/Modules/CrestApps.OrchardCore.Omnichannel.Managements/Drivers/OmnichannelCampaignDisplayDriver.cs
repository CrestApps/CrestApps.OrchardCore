using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelCampaignDisplayDriver : DisplayDriver<OmnichannelCampaign>
{
    private readonly ICatalog<OmnichannelDisposition> _dispositionsCatalog;
    private readonly ICatalog<OmnichannelChannelEndpoint> _channelEndpointsCatalog;
    private readonly INamedCatalog<AIProfile> _aiProfileCatalog;
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    public OmnichannelCampaignDisplayDriver(
        ICatalog<OmnichannelDisposition> dispositionsCatalog,
        ICatalog<OmnichannelChannelEndpoint> channelEndpointsCatalog,
        INamedCatalog<AIProfile> aiProfileCatalog,
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<OmnichannelCampaignDisplayDriver> stringLocalizer)
    {
        _dispositionsCatalog = dispositionsCatalog;
        _channelEndpointsCatalog = channelEndpointsCatalog;
        _aiProfileCatalog = aiProfileCatalog;
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(OmnichannelCampaign campaign, BuildDisplayContext context)
    {
        return CombineAsync(
            View("OmnichannelCampaign_Fields_SummaryAdmin", campaign)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("OmnichannelCampaign_Buttons_SummaryAdmin", campaign)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
            View("OmnichannelCampaign_DefaultMeta_SummaryAdmin", campaign)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
        );
    }

    public override IDisplayResult Edit(OmnichannelCampaign campaign, BuildEditorContext context)
    {
        return Initialize<OmnichannelCampaignViewModel>("OmnichannelCampaignFields_Edit", async model =>
        {
            model.DisplayText = campaign.DisplayText;
            model.Description = campaign.Description;
            model.InteractionType = campaign.InteractionType;
            model.AIProfileName = campaign.AIProfileName;
            model.ChannelEndpoint = campaign.ChannelEndpoint;
            model.AIProfileName = campaign.AIProfileName;
            model.InitialOutboundPromptPattern = campaign.InitialOutboundPromptPattern;

            var dispositions = await _dispositionsCatalog.GetAllAsync();

            model.Dispositions = dispositions.Select(d => new SelectListItem
            {
                Text = d.DisplayText,
                Value = d.Id,
                Selected = campaign.DispositionIds is not null && campaign.DispositionIds.Contains(d.Id)
            }).OrderBy(x => x.Text)
            .ToArray();

            model.AIProfiles = (await _aiProfileCatalog.GetAllAsync()).Select(x => new SelectListItem(x.DisplayText ?? x.Name, x.Name)).OrderBy(x => x.Text);
            model.ChannelEndpoints = (await _channelEndpointsCatalog.GetAllAsync()).Select(x => new SelectListItem(x.DisplayText, x.Id)).OrderBy(x => x.Text);

            model.InteractionTypes =
            [
                new(S["Manual"], nameof(ActivityInteractionType.Manual)),
                new(S["Automated"], nameof(ActivityInteractionType.Automated)),
            ];
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelCampaign campaign, UpdateEditorContext context)
    {
        var model = new OmnichannelCampaignViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["Name is a required field."]);
        }

        var dispositions = await _dispositionsCatalog.GetAllAsync();

        var selectedDispositionIds = (model.Dispositions?.Where(x => x.Selected)
            .Select(d => d.Value) ?? [])
            .Intersect(dispositions.Select(y => y.Id))
            .ToArray();

        if (selectedDispositionIds is null || selectedDispositionIds.Length == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Dispositions), S["At least one Disposition must be selected."]);
        }
        else
        {
            campaign.DispositionIds = selectedDispositionIds;
        }

        if (model.InteractionType == ActivityInteractionType.Automated)
        {
            if (string.IsNullOrEmpty(model.ChannelEndpoint))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ChannelEndpoint), S["Channel endpoint at field is required."]);
            }

            if (string.IsNullOrEmpty(model.AIProfileName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.AIProfileName), S["AI Profile is required for automated activities."]);
            }
            else
            {
                var aiProfile = await _aiProfileCatalog.FindByNameAsync(model.AIProfileName);
                if (aiProfile == null)
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.AIProfileName), S["The selected AI Profile is invalid."]);
                }
            }

            if (string.IsNullOrWhiteSpace(model.InitialOutboundPromptPattern))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.InitialOutboundPromptPattern), S["Initial outbound prompt pattern is a required field for automated activities."]);
            }
            else if (!_liquidTemplateManager.Validate(model.InitialOutboundPromptPattern, out var errors))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.InitialOutboundPromptPattern), S["The initial outbound prompt doesn't contain a valid Liquid expression. Details: {0}", string.Join(' ', errors)]);
            }
        }

        campaign.DisplayText = model.DisplayText?.Trim();
        campaign.Description = model.Description?.Trim();
        campaign.InteractionType = model.InteractionType;
        campaign.ChannelEndpoint = model.ChannelEndpoint;
        campaign.AIProfileName = model.AIProfileName;

        return Edit(campaign, context);
    }
}
