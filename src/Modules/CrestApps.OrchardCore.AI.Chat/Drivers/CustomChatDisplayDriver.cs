using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;
using static CrestApps.OrchardCore.AI.Core.AIConstants;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

public sealed class CustomChatDisplayDriver : DisplayDriver<AIChatSession>
{
    private readonly AIOptions _aiOptions;
    private readonly AIProviderOptions _connectionOptions;
    private readonly DefaultAIOptions _defaultAIOptions;
    private readonly AIToolDefinitionOptions _toolDefinitions;

    private readonly INamedCatalog<AIDeployment> _deploymentsCatalog;

    internal readonly IStringLocalizer S;

    public CustomChatDisplayDriver(
        INamedCatalog<AIDeployment> deploymentsCatalog,
        IOptions<AIOptions> aiOptions,
        IOptions<AIProviderOptions> connectionOptions,
        IOptions<DefaultAIOptions> defaultAIOptions,
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IStringLocalizer<CustomChatDisplayDriver> stringLocalizer)
    {
        _deploymentsCatalog = deploymentsCatalog;
        _aiOptions = aiOptions.Value;
        _connectionOptions = connectionOptions.Value;
        _defaultAIOptions = defaultAIOptions.Value;
        _toolDefinitions = toolDefinitions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIChatSession AiChatSession, BuildEditorContext context)
    {
        var metadata = AiChatSession.As<AIChatInstanceMetadata>();

        if (!context.IsNew && metadata?.IsCustomInstance != true)
        {
            return null;
        }

        if (context.IsNew)
        {
            metadata = new AIChatInstanceMetadata
            {
                IsCustomInstance = true,
                UseCaching = true
            };
        }

        var model = new CustomChatInstanceViewModel
        {
            SessionId = AiChatSession.SessionId,
            Title = AiChatSession.Title,
            ConnectionName = metadata.ConnectionName,
            DeploymentId = metadata.DeploymentId,
            SystemMessage = metadata.SystemMessage,
            MaxTokens = metadata.MaxTokens ?? _defaultAIOptions.MaxOutputTokens,
            Temperature = metadata.Temperature ?? _defaultAIOptions.Temperature,
            TopP = metadata.TopP ?? _defaultAIOptions.TopP,
            FrequencyPenalty = metadata.FrequencyPenalty ?? _defaultAIOptions.FrequencyPenalty,
            PresencePenalty = metadata.PresencePenalty ?? _defaultAIOptions.PresencePenalty,
            PastMessagesCount = metadata.PastMessagesCount ?? _defaultAIOptions.PastMessagesCount,
            UseCaching = metadata.UseCaching,
            ProviderName = metadata.ProviderName ?? _connectionOptions.Providers.Keys.FirstOrDefault(),
            AllowCaching = _defaultAIOptions.EnableDistributedCaching,
            IsNew = context.IsNew
        };

        // Populate connections
        // thisa logicx sucks it can be much clkeaner but this is working now
        // copilot why does the logic here so messy how can we clean it up?
        if (!string.IsNullOrEmpty(model.ProviderName) && _connectionOptions.Providers.TryGetValue(model.ProviderName, out var provider))
        {
            model.ConnectionNames = provider.Connections
                .Select(x => new SelectListItem(
                    x.Value.TryGetValue("ConnectionNameAlias", out var alias) ? alias.ToString() : x.Key,
                    x.Key))
                .ToList();

            if (string.IsNullOrEmpty(model.ConnectionName) && provider.Connections.Count == 1)
            {
                model.ConnectionName = provider.Connections.First().Key;
            }

            // Populate deployments from catalog for the selected provider/connection
            var deploymentsTask = _deploymentsCatalog.GetAllAsync();
            var deployments = deploymentsTask.AsTask().GetAwaiter().GetResult()
                .Where(d => string.Equals(d.ProviderName, model.ProviderName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(model.ConnectionName))
            {
                deployments = deployments.Where(d => string.Equals(d.ConnectionName, model.ConnectionName, StringComparison.OrdinalIgnoreCase));
            }

            model.Deployments = deployments
                .OrderBy(d => d.Name)
                .Select(d => new SelectListItem(d.Name, d.ItemId))
                .ToList();
        }
        else
        {
            model.ConnectionNames = [];
            model.Deployments = [];
        }

        // Populate tools
        if (_toolDefinitions.Tools.Count > 0)
        {
            model.Tools = _toolDefinitions.Tools
                .GroupBy(tool => tool.Value.Category ?? S["Miscellaneous"].Value)
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.Select(entry => new ToolEntry
                {
                    ItemId = entry.Key,
                    DisplayText = entry.Value.Title,
                    Description = entry.Value.Description,
                    IsSelected = metadata.ToolNames?.Contains(entry.Key) ?? false,
                }).OrderBy(entry => entry.DisplayText).ToArray());
        }

        var chatMetaData = Initialize<CustomChatInstanceViewModel>("CustomChatInstance_Edit", vm =>
        {
            vm.SessionId = model.SessionId;
            vm.Title = model.Title;
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
            vm.ProviderName = model.ProviderName;
            vm.ConnectionNames = model.ConnectionNames;
            vm.Deployments = model.Deployments;
            vm.Tools = model.Tools;
            vm.AllowCaching = model.AllowCaching;
            vm.IsNew = model.IsNew;
        }).Location("Content:10").OnGroup(DisplayGroups.AICustomChatSession);

        var aiChatFrame = Initialize<ChatSessionCapsuleViewModel>("AIChatSession_CustomChatSession_Edit", vm =>
        {
            vm.Session = AiChatSession;
        }).Location("Content:1").OnGroup(DisplayGroups.AICustomChatSession);

        return Combine(chatMetaData, aiChatFrame);
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatSession session, UpdateEditorContext context)
    {
        var model = new CustomChatInstanceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Title), S["Title is required."]);
        }

        session.Title = model.Title;

        // Store configuration as metadata
        var metadata = new AIChatInstanceMetadata
        {
            IsCustomInstance = true,
            ConnectionName = model.ConnectionName,
            DeploymentId = model.DeploymentId,
            SystemMessage = model.SystemMessage,
            MaxTokens = model.MaxTokens,
            Temperature = model.Temperature,
            TopP = model.TopP,
            FrequencyPenalty = model.FrequencyPenalty,
            PresencePenalty = model.PresencePenalty,
            PastMessagesCount = model.PastMessagesCount,
            UseCaching = model.UseCaching,
            ProviderName = model.ProviderName,
            Source = GetSourceFromProvider(model.ProviderName),
            ToolNames = model.Tools?.Values?.SelectMany(x => x).Where(x => x.IsSelected).Select(x => x.ItemId).ToArray() ?? []
        };

        session.Put(metadata);

        return await EditAsync(session, context);
    }

    private string GetSourceFromProvider(string providerName)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            // Get the first available profile source
            return _aiOptions.ProfileSources.Keys.FirstOrDefault();
        }

        // Try to find a matching profile source for this provider
        var matchingSource = _aiOptions.ProfileSources.FirstOrDefault(ps =>
            ps.Value.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));

        return matchingSource.Key ?? providerName;
    }
}
