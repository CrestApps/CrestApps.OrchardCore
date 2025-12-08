using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver for custom chat instance configuration.
/// </summary>
public sealed class CustomChatInstanceConfigurationDisplayDriver : DisplayDriver<AIChatSession>
{
    private readonly AIOptions _aiOptions;
    private readonly AIProviderOptions _connectionOptions;
    private readonly DefaultAIOptions _defaultAIOptions;
    private readonly AIToolDefinitionOptions _toolDefinitions;

    internal readonly IStringLocalizer S;

    public CustomChatInstanceConfigurationDisplayDriver(
        IOptions<AIOptions> aiOptions,
        IOptions<AIProviderOptions> connectionOptions,
        IOptions<DefaultAIOptions> defaultAIOptions,
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IStringLocalizer<CustomChatInstanceConfigurationDisplayDriver> stringLocalizer)
    {
        _aiOptions = aiOptions.Value;
        _connectionOptions = connectionOptions.Value;
        _defaultAIOptions = defaultAIOptions.Value;
        _toolDefinitions = toolDefinitions.Value;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIChatSession session, BuildDisplayContext context)
    {
        var metadata = session.As<AIChatInstanceMetadata>();

        if (metadata?.IsCustomInstance != true)
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return Task.FromResult<IDisplayResult>(
            View("CustomChatInstance_SummaryAdmin", session).Location("Content:1")
        );
    }

    public override IDisplayResult Edit(AIChatSession session, BuildEditorContext context)
    {
        var metadata = session.As<AIChatInstanceMetadata>();

        if (!context.IsNew && metadata?.IsCustomInstance != true)
        {
            return null;
        }

        // For new sessions, metadata won't exist yet, so we initialize defaults
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
            SessionId = session.SessionId,
            Title = session.Title,
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
        if (!string.IsNullOrEmpty(model.ProviderName) && _connectionOptions.Providers.TryGetValue(model.ProviderName, out var provider))
        {
            model.ConnectionNames = provider.Connections
                .Select(x => new SelectListItem(
                    x.Value.TryGetValue("ConnectionNameAlias", out var alias) ? alias.ToString() : x.Key,
                    x.Key))
                .ToList();

            // Set default connection if not set and only one available
            if (string.IsNullOrEmpty(model.ConnectionName) && provider.Connections.Count == 1)
            {
                model.ConnectionName = provider.Connections.First().Key;
            }

            // Populate deployments if connection is set
            if (!string.IsNullOrEmpty(model.ConnectionName) && provider.Connections.TryGetValue(model.ConnectionName, out var connectionConfig))
            {
                if (connectionConfig.TryGetValue("Deployments", out var deploymentsObj) && deploymentsObj is IDictionary<string, object> deployments)
                {
                    model.Deployments = deployments.Select(d => new SelectListItem(d.Value.ToString(), d.Key)).ToList();
                }
            }
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

        return Initialize<CustomChatInstanceViewModel>("CustomChatInstance_Edit", m =>
        {
            m.SessionId = model.SessionId;
            m.Title = model.Title;
            m.ConnectionName = model.ConnectionName;
            m.DeploymentId = model.DeploymentId;
            m.SystemMessage = model.SystemMessage;
            m.MaxTokens = model.MaxTokens;
            m.Temperature = model.Temperature;
            m.TopP = model.TopP;
            m.FrequencyPenalty = model.FrequencyPenalty;
            m.PresencePenalty = model.PresencePenalty;
            m.PastMessagesCount = model.PastMessagesCount;
            m.UseCaching = model.UseCaching;
            m.ProviderName = model.ProviderName;
            m.ConnectionNames = model.ConnectionNames;
            m.Deployments = model.Deployments;
            m.Tools = model.Tools;
            m.AllowCaching = model.AllowCaching;
            m.IsNew = model.IsNew;
        }).Location("Content:1");
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
