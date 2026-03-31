using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

public sealed class AICompletionWithConfigTaskDisplayDriver : ActivityDisplayDriver<AICompletionWithConfigTask, AICompletionWithConfigTaskViewModel>
{
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly DefaultAIOptions _defaultAIOptions;
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    public AICompletionWithConfigTaskDisplayDriver(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IAIDeploymentManager deploymentManager,
        DefaultAIOptions defaultAIOptions,
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<AICompletionFromProfileTaskDisplayDriver> stringLocalizer)
    {
        _toolDefinitions = toolDefinitions.Value;
        _deploymentManager = deploymentManager;
        _defaultAIOptions = defaultAIOptions;
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AICompletionWithConfigTask activity, BuildEditorContext context)
    {
        var contents = Initialize<AICompletionWithConfigTaskViewModel>(ActivityName + "_Fields_Edit", async model =>
        {
            model.PromptTemplate = activity.PromptTemplate;
            model.ResultPropertyName = activity.ResultPropertyName;
            model.DeploymentName = await NormalizeDeploymentSelectorAsync(activity.DeploymentName);

            model.MaxTokens = context.IsNew ? _defaultAIOptions.MaxOutputTokens : activity.MaxTokens;
            model.Temperature = context.IsNew ? _defaultAIOptions.Temperature : activity.Temperature;
            model.TopP = context.IsNew ? _defaultAIOptions.TopP : activity.TopP;
            model.FrequencyPenalty = context.IsNew ? _defaultAIOptions.FrequencyPenalty : activity.FrequencyPenalty;
            model.PresencePenalty = context.IsNew ? _defaultAIOptions.PresencePenalty : activity.PresencePenalty;
            model.SystemMessage = activity.SystemMessage;
            model.DeploymentNames = BuildGroupedDeploymentItems(
                await _deploymentManager.GetByTypeAsync(AIDeploymentType.Chat));

        }).Location("Content");

        if (_toolDefinitions.Tools.Count == 0)
        {
            return contents;
        }

        var tools = Initialize<EditProfileToolsViewModel>("EditProfileTools_Edit", model =>
        {
            model.Tools = _toolDefinitions.Tools
                .Where(tool => !tool.Value.IsSystemTool)
                .GroupBy(tool => tool.Value.Category ?? S["Miscellaneous"])
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.Select(entry => new ToolEntry
                {
                    ItemId = entry.Key,
                    DisplayText = entry.Value.Title,
                    Description = entry.Value.Description,
                    IsSelected = activity.ToolNames?.Contains(entry.Key) ?? false,
                }).OrderBy(entry => entry.DisplayText).ToArray());
        }).Location("Content:7#Capabilities;8");

        return Combine(contents, tools);
    }

    public override async Task<IDisplayResult> UpdateAsync(AICompletionWithConfigTask activity, UpdateEditorContext context)
    {
        var model = new AICompletionWithConfigTaskViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.DeploymentName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DeploymentName), S["The Deployment is required."]);
        }
        else if (await FindDeploymentAsync(model.DeploymentName) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DeploymentName), S["The Deployment is invalid."]);
        }

        if (string.IsNullOrEmpty(model.PromptTemplate))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PromptTemplate), S["The Prompt template is required."]);
        }
        else if (!_liquidTemplateManager.Validate(model.PromptTemplate, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PromptTemplate), S["The Prompt template is invalid."]);
        }

        if (string.IsNullOrWhiteSpace(model.ResultPropertyName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ResultPropertyName), S["The Property name is required."]);
        }

        activity.PromptTemplate = model.PromptTemplate;
        activity.ResultPropertyName = model.ResultPropertyName?.Trim();
        activity.DeploymentName = model.DeploymentName?.Trim();

        activity.MaxTokens = model.MaxTokens;
        activity.Temperature = model.Temperature;
        activity.TopP = model.TopP;
        activity.FrequencyPenalty = model.FrequencyPenalty;
        activity.PresencePenalty = model.PresencePenalty;
        activity.SystemMessage = model.SystemMessage;

        if (_toolDefinitions.Tools.Count > 0)
        {
            var toolsModel = new EditProfileToolsViewModel();

            await context.Updater.TryUpdateModelAsync(toolsModel, Prefix);

            var selectedToolKeys = toolsModel.Tools?.Values?.SelectMany(x => x).Where(x => x.IsSelected).Select(x => x.ItemId);

            if (selectedToolKeys is null || !selectedToolKeys.Any())
            {
                activity.ToolNames = [];
            }
            else
            {
                activity.ToolNames = _toolDefinitions.Tools.Keys
                    .Intersect(selectedToolKeys)
                    .ToArray();
            }
        }

        return Edit(activity, context);
    }

    private static IEnumerable<SelectListItem> BuildGroupedDeploymentItems(IEnumerable<AIDeployment> deployments)
    {
        var groups = new Dictionary<string, SelectListGroup>(StringComparer.OrdinalIgnoreCase);

        return deployments
            .OrderBy(d => d.ConnectionNameAlias ?? d.ConnectionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d =>
            {
                SelectListGroup group = null;
                var groupKey = d.ConnectionNameAlias ?? d.ConnectionName;

                if (!string.IsNullOrEmpty(groupKey) && !groups.TryGetValue(groupKey, out group))
                {
                    group = new SelectListGroup { Name = groupKey };
                    groups[groupKey] = group;
                }

                var label = string.Equals(d.Name, d.ModelName, StringComparison.OrdinalIgnoreCase)
                    ? d.Name
                    : $"{d.Name} ({d.ModelName})";

                return new SelectListItem(label, d.Name) { Group = group };
            });
    }

    private async Task<AIDeployment> FindDeploymentAsync(string selector)
    {
        var deployment = await _deploymentManager.FindByNameAsync(selector);

        if (deployment != null)
        {
            return deployment;
        }

        return await _deploymentManager.FindByIdAsync(selector);
    }

    private async Task<string> NormalizeDeploymentSelectorAsync(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return selector;
        }

        var deployment = await FindDeploymentAsync(selector);

        return deployment?.Name ?? selector;
    }
}
