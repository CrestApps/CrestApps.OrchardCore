using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
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
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly AIProviderOptions _aiProviderOptions;
    private readonly DefaultAIOptions _defaultAIOptions;

    internal readonly IStringLocalizer S;

    public OmnichannelCampaignDisplayDriver(
        ICatalog<OmnichannelDisposition> dispositionsCatalog,
        ICatalog<OmnichannelChannelEndpoint> channelEndpointsCatalog,
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IOptions<AIProviderOptions> aiProviderOptions,
        IOptions<DefaultAIOptions> defaultAIOptions,
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<OmnichannelCampaignDisplayDriver> stringLocalizer)
    {
        _dispositionsCatalog = dispositionsCatalog;
        _channelEndpointsCatalog = channelEndpointsCatalog;
        _toolDefinitions = toolDefinitions.Value;
        _aiProviderOptions = aiProviderOptions.Value;
        _defaultAIOptions = defaultAIOptions.Value;
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
            model.Channel = campaign.Channel;
            model.ChannelEndpointId = campaign.ChannelEndpointId;
            model.InitialOutboundPromptPattern = campaign.InitialOutboundPromptPattern;
            model.CampaignGoal = campaign.CampaignGoal;

            // AI config
            model.ProviderName = campaign.ProviderName;
            model.ConnectionName = campaign.ConnectionName;
            model.DeploymentName = campaign.DeploymentName;
            model.SystemMessage = campaign.SystemMessage;
            model.MaxTokens = context.IsNew ? _defaultAIOptions.MaxOutputTokens : campaign.MaxTokens;
            model.Temperature = context.IsNew ? _defaultAIOptions.Temperature : campaign.Temperature;
            model.TopP = context.IsNew ? _defaultAIOptions.TopP : campaign.TopP;
            model.FrequencyPenalty = context.IsNew ? _defaultAIOptions.FrequencyPenalty : campaign.FrequencyPenalty;
            model.PresencePenalty = context.IsNew ? _defaultAIOptions.PresencePenalty : campaign.PresencePenalty;
            model.AllowAIToUpdateContact = !context.IsNew && campaign.AllowAIToUpdateContact;
            model.AllowAIToUpdateSubject = context.IsNew || campaign.AllowAIToUpdateSubject;

            var dispositions = await _dispositionsCatalog.GetAllAsync();

            model.Dispositions = dispositions.Select(d => new SelectListItem
            {
                Text = d.DisplayText,
                Value = d.ItemId,
                Selected = campaign.DispositionIds is not null && campaign.DispositionIds.Contains(d.ItemId)
            }).OrderBy(x => x.Text)
            .ToArray();

            model.Providers = _aiProviderOptions.Providers.Select(provider => new SelectListItem(provider.Key, provider.Key));

            model.Channels =
            [
                new(S["Phone"], OmnichannelConstants.Channels.Phone),
                new(S["SMS"], OmnichannelConstants.Channels.Sms),
                new(S["Email"], OmnichannelConstants.Channels.Email),
            ];
            model.ChannelEndpoints = (await _channelEndpointsCatalog.GetAllAsync()).Select(x => new SelectListItem(x.DisplayText, x.ItemId)).OrderBy(x => x.Text);

            model.InteractionTypes =
            [
                new(S["Manual"], nameof(ActivityInteractionType.Manual)),
                new(S["Automated"], nameof(ActivityInteractionType.Automated)),
            ];

            if (_toolDefinitions.Tools.Count > 0)
            {
                model.Tools = _toolDefinitions.Tools
                .GroupBy(tool => tool.Value.Category ?? S["Miscellaneous"])
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.Select(entry => new ToolEntry
                {
                    ItemId = entry.Key,
                    DisplayText = entry.Value.Title,
                    Description = entry.Value.Description,
                    IsSelected = campaign.ToolNames?.Contains(entry.Key) ?? false,
                }).OrderBy(entry => entry.DisplayText).ToArray());
            }
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
            .Intersect(dispositions.Select(y => y.ItemId))
            .ToArray();

        if (selectedDispositionIds is null || selectedDispositionIds.Length == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Dispositions), S["At least one Disposition must be selected."]);
        }
        else
        {
            campaign.DispositionIds = selectedDispositionIds;
        }

        if (string.IsNullOrEmpty(model.Channel))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Channel), S["Channel field is required."]);
        }

        if (model.InteractionType == ActivityInteractionType.Automated)
        {
            if (string.IsNullOrEmpty(model.ChannelEndpointId))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ChannelEndpointId), S["Channel endpoint field is required for automated activities."]);
            }

            // Campaign goal is required for automated type
            if (string.IsNullOrWhiteSpace(model.CampaignGoal))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.CampaignGoal), S["Campaign goal is required for automated activities."]);
            }

            // Provider validation
            if (string.IsNullOrEmpty(model.ProviderName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProviderName), S["The Provider is required."]);
            }
            else if (!_aiProviderOptions.Providers.TryGetValue(model.ProviderName, out _))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProviderName), S["The Provider is invalid."]);
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

        // Map fields regardless of type so values are preserved when switching back/forth in UI
        campaign.DisplayText = model.DisplayText?.Trim();
        campaign.Description = model.Description?.Trim();
        campaign.InteractionType = model.InteractionType;
        campaign.Channel = model.Channel;
        campaign.ChannelEndpointId = model.ChannelEndpointId;
        campaign.InitialOutboundPromptPattern = model.InitialOutboundPromptPattern;
        campaign.CampaignGoal = model.CampaignGoal;

        // AI config
        campaign.ProviderName = model.ProviderName;
        campaign.ConnectionName = model.ConnectionName;
        campaign.DeploymentName = model.DeploymentName;
        campaign.SystemMessage = model.SystemMessage;
        campaign.MaxTokens = model.MaxTokens;
        campaign.Temperature = model.Temperature;
        campaign.TopP = model.TopP;
        campaign.FrequencyPenalty = model.FrequencyPenalty;
        campaign.PresencePenalty = model.PresencePenalty;
        campaign.AllowAIToUpdateContact = model.AllowAIToUpdateContact;
        campaign.AllowAIToUpdateSubject = model.AllowAIToUpdateSubject;

        if (_toolDefinitions.Tools.Count > 0)
        {
            // Bind tools selection
            var toolsModel = new OmnichannelCampaignViewModel();
            await context.Updater.TryUpdateModelAsync(toolsModel, Prefix);

            var selectedToolKeys = toolsModel.Tools?.Values?.SelectMany(x => x).Where(x => x.IsSelected).Select(x => x.ItemId);

            campaign.ToolNames = selectedToolKeys is null || !selectedToolKeys.Any()
                ? []
                : _toolDefinitions.Tools.Keys
                    .Intersect(selectedToolKeys)
                    .ToArray();
        }

        return Edit(campaign, context);
    }
}
