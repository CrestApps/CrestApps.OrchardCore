using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

internal sealed class AICustomChatInstanceDisplayDriver : DisplayDriver<AICustomChatInstance>
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly AIProviderOptions _providerOptions;
    private readonly AIToolDefinitionOptions _toolDefinitions;

    internal readonly IStringLocalizer S;

    public AICustomChatInstanceDisplayDriver(
        IAIDeploymentManager deploymentManager,
        IOptions<AIProviderOptions> providerOptions,
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IStringLocalizer<AICustomChatInstanceDisplayDriver> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _providerOptions = providerOptions.Value;
        _toolDefinitions = toolDefinitions.Value;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AICustomChatInstance instance, BuildDisplayContext context)
    {
        return CombineAsync(
            View("AICustomChatInstance_Fields_SummaryAdmin", instance).Location("Content:1"),
            View("AICustomChatInstance_Buttons_SummaryAdmin", instance).Location("Actions:5")
        );
    }

    public override IDisplayResult Edit(AICustomChatInstance instance, BuildEditorContext context)
    {
        return Initialize<EditCustomChatInstanceViewModel>("AICustomChatInstance_Edit", async model =>
        {
            model.DisplayText = instance.DisplayText;
            model.ConnectionName = instance.ConnectionName;
            model.DeploymentId = instance.DeploymentId;
            model.SystemMessage = instance.SystemMessage;
            model.MaxTokens = instance.MaxTokens;
            model.Temperature = instance.Temperature;
            model.TopP = instance.TopP;
            model.FrequencyPenalty = instance.FrequencyPenalty;
            model.PresencePenalty = instance.PresencePenalty;
            model.PastMessagesCount = instance.PastMessagesCount;
            model.IsNew = context.IsNew;

            await PopulateDropdownsAsync(model, instance);
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AICustomChatInstance instance, UpdateEditorContext context)
    {
        var model = new EditCustomChatInstanceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["Title is required."]);
        }

        instance.DisplayText = model.DisplayText?.Trim();
        instance.ConnectionName = model.ConnectionName;
        instance.DeploymentId = model.DeploymentId;
        instance.SystemMessage = model.SystemMessage;
        instance.MaxTokens = model.MaxTokens;
        instance.Temperature = model.Temperature;
        instance.TopP = model.TopP;
        instance.FrequencyPenalty = model.FrequencyPenalty;
        instance.PresencePenalty = model.PresencePenalty;
        instance.PastMessagesCount = model.PastMessagesCount;

        var selectedToolKeys = model.Tools?.Values?.SelectMany(x => x).Where(x => x.IsSelected).Select(x => x.ItemId);

        if (selectedToolKeys is null || !selectedToolKeys.Any())
        {
            instance.ToolNames = [];
        }
        else
        {
            instance.ToolNames = _toolDefinitions.Tools.Keys
                .Intersect(selectedToolKeys)
                .ToList();
        }

        return Edit(instance, context);
    }

    private async Task PopulateDropdownsAsync(EditCustomChatInstanceViewModel model, AICustomChatInstance instance)
    {
        var connectionNames = new List<SelectListItem>();
        string providerName = null;

        foreach (var provider in _providerOptions.Providers)
        {
            foreach (var connection in provider.Value.Connections)
            {
                var displayName = connection.Value.TryGetValue("ConnectionNameAlias", out var alias)
                    ? alias.ToString()
                    : connection.Key;
                connectionNames.Add(new SelectListItem(displayName, connection.Key));

                if (string.IsNullOrEmpty(providerName))
                {
                    providerName = provider.Key;
                }
            }
        }

        model.ConnectionNames = connectionNames;
        model.ProviderName = providerName;

        var connectionName = model.ConnectionName;
        if (string.IsNullOrEmpty(connectionName) && connectionNames.Count > 0)
        {
            connectionName = connectionNames.First().Value;
        }

        if (!string.IsNullOrEmpty(connectionName) && !string.IsNullOrEmpty(providerName))
        {
            var deployments = await _deploymentManager.GetAllAsync(providerName, connectionName);
            model.Deployments = deployments.Select(d => new SelectListItem(d.Name, d.ItemId));
        }
        else
        {
            model.Deployments = [];
        }

        if (_toolDefinitions.Tools.Count > 0)
        {
            var selectedTools = instance?.ToolNames ?? [];
            model.Tools = _toolDefinitions.Tools
                .GroupBy(tool => tool.Value.Category ?? S["Miscellaneous"].Value)
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.Select(entry => new ToolEntry
                {
                    ItemId = entry.Key,
                    DisplayText = entry.Value.Title,
                    Description = entry.Value.Description,
                    IsSelected = selectedTools.Contains(entry.Key),
                }).OrderBy(entry => entry.DisplayText).ToArray());
        }
    }
}
