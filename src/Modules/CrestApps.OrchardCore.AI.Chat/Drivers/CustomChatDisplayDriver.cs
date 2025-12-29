using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using YesSql;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

public sealed class CustomChatDisplayDriver : ContentPartDisplayDriver<CustomChatPart>
{
    private readonly AIProviderOptions _providerOptions;
    private readonly DefaultAIOptions _defaultOptions;
    private readonly ISession _session;
    private readonly INamedCatalog<AIDeployment> _deployments;
    private readonly IContentManager _contentManager;
    private readonly IStringLocalizer S;

    public CustomChatDisplayDriver(
        ISession session,
        IContentManager contentManager,
        INamedCatalog<AIDeployment> deployments,
        IOptions<AIProviderOptions> providerOptions,
        IOptions<DefaultAIOptions> defaultOptions,
        IStringLocalizer<CustomChatDisplayDriver> localizer)
    {
        _session = session;
        _contentManager = contentManager;
        _deployments = deployments;
        _providerOptions = providerOptions.Value;
        _defaultOptions = defaultOptions.Value;
        S = localizer;
    }

    public override async Task<IDisplayResult> EditAsync(CustomChatPart part, BuildPartEditorContext context)
    {
        if (!part.IsCustomInstance && !context.IsNew)
        {
            return null;
        }

        var model = new CustomChatViewModel
        {
            CustomChatInstanceId = part.CustomChatInstanceId,
            SessionId = part.SessionId,
            Title = part.Title,
            ProviderName = part.ProviderName ?? _providerOptions.Providers.Keys.FirstOrDefault(),
            ConnectionName = part.ConnectionName,
            DeploymentId = part.DeploymentId,
            SystemMessage = part.SystemMessage,
            MaxTokens = part.MaxTokens > 0 ? part.MaxTokens : _defaultOptions.MaxOutputTokens,
            Temperature = part.Temperature > 0 ? part.Temperature : _defaultOptions.Temperature,
            TopP = part.TopP > 0 ? part.TopP : _defaultOptions.TopP,
            FrequencyPenalty = part.FrequencyPenalty,
            PresencePenalty = part.PresencePenalty,
            PastMessagesCount = part.PastMessagesCount > 0 ? part.PastMessagesCount : _defaultOptions.PastMessagesCount,
            UseCaching = part.UseCaching,
            AllowCaching = _defaultOptions.EnableDistributedCaching,
            IsNew = context.IsNew
        };

        if (!string.IsNullOrEmpty(model.ProviderName) && _providerOptions.Providers.TryGetValue(model.ProviderName, out var provider))
        {
            model.ConnectionNames = provider.Connections.Select(x => new SelectListItem
            {
                Value = x.Key,
                Text = x.Key
            }).OrderBy(x => x.Text).ToList();

            var deployments = (await _deployments.GetAllAsync()).Where(d => d.ProviderName == model.ProviderName);

            if (!string.IsNullOrEmpty(model.ConnectionName))
            {
                deployments = deployments.Where(d => d.ConnectionName == model.ConnectionName);
            }

            model.Deployments = deployments.OrderBy(d => d.Name).Select(d => new SelectListItem(d.Name, d.ItemId)).ToList();
        }

        var toolDocument = await _session.Query<DictionaryDocument<AIToolInstance>>().FirstOrDefaultAsync();

        if (toolDocument != null)
        {
            model.Tools = toolDocument.Records.Values.GroupBy(x => x.Source ?? S["Miscellaneous"].Value)
                .ToDictionary(x => x.Key, x => x.Select(x => new ToolEntry
                {
                    ItemId = x.ItemId,
                    DisplayText = x.DisplayText,
                    IsSelected = part.ToolNames?.Contains(x.ItemId) ?? false
                }).ToArray());
        }
        else
        {
            model.Tools = null;
        }

        var MetaData = Initialize<CustomChatViewModel>("CustomChatSessionSettings_Edit", vm =>
        {
            vm.CustomChatInstanceId = model.CustomChatInstanceId;
            vm.SessionId = model.SessionId;
            vm.Title = model.Title;
            vm.ProviderName = model.ProviderName;
            vm.ConnectionNames = model.ConnectionNames;
            vm.ConnectionName = model.ConnectionName;
            vm.DeploymentId = model.DeploymentId;
            vm.SystemMessage = model.SystemMessage;
            vm.MaxTokens = model.MaxTokens;
            vm.Temperature = model.Temperature;
            vm.TopP = model.TopP;
            vm.FrequencyPenalty = model.FrequencyPenalty;
            vm.PresencePenalty = model.PresencePenalty;
            vm.PastMessagesCount = model.PastMessagesCount;
            vm.UseCaching = model.UseCaching;
            vm.AllowCaching = model.AllowCaching;
            vm.Tools = model.Tools;
            vm.Deployments = model.Deployments;
            vm.IsNew = model.IsNew;
        }).Location("Content:1#Settings");

        var Chat = Initialize<ChatSessionCapsuleViewModel>("CustomChatSession_Edit", vm =>
        {
            vm.CustomChatSession = new CustomChatSession
            {
                SessionId = part.SessionId,
                CustomChatInstanceId = part.CustomChatInstanceId,
                UserId = part.UserId,
                Title = part.Title,
                Source = part.Source,
                CreatedUtc = part.CreatedUtc,
                Prompts = []
            };

            vm.IsNew = context.IsNew;
        }).Location("Content:2#Chat");

        var Tools = Initialize<CustomChatViewModel>("CustomChatSessionTools_Edit", vm =>
        {
            vm.CustomChatInstanceId = model.CustomChatInstanceId;
            vm.SessionId = model.SessionId;
            vm.Tools = model.Tools;
        })
        .Location("Content:3#Tools");

        return Combine(Chat, MetaData, Tools);
    }

    public override async Task<IDisplayResult> UpdateAsync(CustomChatPart part, UpdatePartEditorContext context)
    {
        var model = new CustomChatViewModel();

        part.IsCustomInstance = true;

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Title), S["Title is required."]);
        }

        part.Title = model.Title;
        part.ProviderName = model.ProviderName;
        part.ConnectionName = model.ConnectionName;
        part.DeploymentId = model.DeploymentId;
        part.SystemMessage = model.SystemMessage;
        part.Source = model.ProviderName;
        part.MaxTokens = model.MaxTokens ?? _defaultOptions.MaxOutputTokens;
        part.Temperature = model.Temperature ?? _defaultOptions.Temperature;
        part.TopP = model.TopP ?? _defaultOptions.TopP;
        part.FrequencyPenalty = model.FrequencyPenalty ?? _defaultOptions.FrequencyPenalty;
        part.PresencePenalty = model.PresencePenalty ?? _defaultOptions.PresencePenalty;
        part.PastMessagesCount = model.PastMessagesCount ?? _defaultOptions.PastMessagesCount;
        part.UseCaching = model.UseCaching;
        part.ToolNames = model.Tools?.SelectMany(x => x.Value).Where(x => x.IsSelected).Select(x => x.ItemId).ToArray() ?? [];

        return await EditAsync(part, context);
    }
}
