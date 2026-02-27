using System.Text.Json;
using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Prompting.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Prompting.Drivers;

public sealed class AIProfilePromptSelectionDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IAITemplateService _aiTemplateService;

    public AIProfilePromptSelectionDisplayDriver(IAITemplateService aiTemplateService)
    {
        _aiTemplateService = aiTemplateService;
    }

    public override async Task<IDisplayResult> EditAsync(AIProfile profile, BuildEditorContext context)
    {
        var templates = await _aiTemplateService.ListAsync();
        var listableTemplates = templates.Where(t => t.Metadata.IsListable).ToList();

        if (listableTemplates.Count == 0)
        {
            return null;
        }

        return Initialize<AITemplateSelectionViewModel>("AIProfilePromptSelection_Edit", model =>
        {
            var promptMetadata = profile.As<PromptTemplateMetadata>();

            model.SelectedPromptId = promptMetadata.TemplateId;
            model.PromptParameters = SerializeParameters(promptMetadata.Parameters);

            PopulateViewModel(model, listableTemplates);
        }).Location("Content:9");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new AITemplateSelectionViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var promptMetadata = new PromptTemplateMetadata();

        if (!string.IsNullOrEmpty(model.SelectedPromptId))
        {
            promptMetadata.TemplateId = model.SelectedPromptId;

            if (!string.IsNullOrEmpty(model.PromptParameters))
            {
                var parameters = ParseAndValidateParameters(model.PromptParameters);

                if (parameters != null)
                {
                    var template = await _aiTemplateService.GetAsync(model.SelectedPromptId);
                    var invalidKeys = GetInvalidParameterKeys(parameters, template);

                    if (invalidKeys.Count > 0)
                    {
                        context.Updater.ModelState.AddModelError(
                            Prefix + '.' + nameof(model.PromptParameters),
                            $"The following parameter keys are not supported by this template: {string.Join(", ", invalidKeys)}");
                    }
                    else
                    {
                        promptMetadata.Parameters = parameters;
                    }
                }
                else
                {
                    context.Updater.ModelState.AddModelError(
                        Prefix + '.' + nameof(model.PromptParameters),
                        "The parameters must be valid JSON with string key-value pairs. Example: {\"key1\": \"value1\"}");
                }
            }
        }

        profile.Put(promptMetadata);

        return await EditAsync(profile, context);
    }

    internal static void PopulateViewModel(AITemplateSelectionViewModel model, IReadOnlyList<global::CrestApps.AI.Prompting.Models.AITemplate> listableTemplates)
    {
        var groups = new Dictionary<string, SelectListGroup>(StringComparer.OrdinalIgnoreCase);

        var sorted = listableTemplates
            .OrderBy(t => t.Metadata.Category ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Metadata.Title ?? t.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var template in sorted)
        {
            var categoryName = template.Metadata.Category ?? "General";

            if (!groups.TryGetValue(categoryName, out var group))
            {
                group = new SelectListGroup { Name = categoryName };
                groups[categoryName] = group;
            }

            model.AvailablePrompts.Add(new SelectListItem
            {
                Text = template.Metadata.Title ?? template.Id,
                Value = template.Id,
                Group = group,
            });

            if (!string.IsNullOrEmpty(template.Metadata.Description))
            {
                model.PromptDescriptions[template.Id] = template.Metadata.Description;
            }

            if (template.Metadata.Parameters is { Count: > 0 })
            {
                model.PromptParameterDescriptors[template.Id] = template.Metadata.Parameters;
            }
        }

        model.AvailableGroups = groups.Values.ToList();
    }

    internal static Dictionary<string, object> ParseAndValidateParameters(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                parameters[property.Name] = property.Value.GetString();
            }

            return parameters;
        }
        catch
        {
            return null;
        }
    }

    internal static string SerializeParameters(Dictionary<string, object> parameters)
    {
        if (parameters is not { Count: > 0 })
        {
            return null;
        }

        return JsonSerializer.Serialize(parameters);
    }

    internal static List<string> GetInvalidParameterKeys(
        Dictionary<string, object> providedParameters,
        global::CrestApps.AI.Prompting.Models.AITemplate template)
    {
        var invalidKeys = new List<string>();

        if (template?.Metadata.Parameters is not { Count: > 0 })
        {
            // Template declares no parameters; all provided keys are invalid.
            return [.. providedParameters.Keys];
        }

        var declaredNames = new HashSet<string>(
            template.Metadata.Parameters.Select(p => p.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var key in providedParameters.Keys)
        {
            if (!declaredNames.Contains(key))
            {
                invalidKeys.Add(key);
            }
        }

        return invalidKeys;
    }
}
