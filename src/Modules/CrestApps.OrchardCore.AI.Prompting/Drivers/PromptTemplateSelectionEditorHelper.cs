using System.Text.Json;
using CrestApps.AI.Prompting.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Prompting.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Prompting.Drivers;

internal static class PromptTemplateSelectionEditorHelper
{
    internal static async Task PopulateViewModelAsync(
        AITemplateSelectionViewModel model,
        PromptTemplateMetadata promptMetadata,
        PromptTemplateSelectionService promptTemplateSelectionService)
    {
        model.PromptTemplates = (promptMetadata.Templates ?? [])
            .Where(selection => !string.IsNullOrWhiteSpace(selection.TemplateId))
            .Select(selection => new PromptTemplateSelectionItemViewModel
            {
                TemplateId = selection.TemplateId,
                PromptParameters = SerializeParameters(selection.Parameters),
            })
            .ToList();

        model.AvailablePrompts = (await promptTemplateSelectionService.ListAsync())
            .Where(template => template.Metadata.IsListable)
            .OrderBy(template => template.Metadata.Category ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(template => template.Metadata.Title ?? template.Id, StringComparer.OrdinalIgnoreCase)
            .Select(template => new PromptTemplateOptionViewModel
            {
                TemplateId = template.Id,
                Title = template.Metadata.Title ?? template.Id,
                Description = template.Metadata.Description,
                Category = template.Metadata.Category ?? "General",
                Parameters = template.Metadata.Parameters ?? [],
            })
            .ToList();
    }

    internal static async Task<PromptTemplateMetadata> BuildMetadataAsync(
        AITemplateSelectionViewModel model,
        PromptTemplateSelectionService promptTemplateSelectionService,
        ModelStateDictionary modelState,
        string prefix)
    {
        var metadata = new PromptTemplateMetadata();
        var selections = new List<PromptTemplateSelectionEntry>();

        if (model.PromptTemplates is null)
        {
            metadata.SetSelections(selections);

            return metadata;
        }

        for (var index = 0; index < model.PromptTemplates.Count; index++)
        {
            var item = model.PromptTemplates[index];

            if (string.IsNullOrWhiteSpace(item.TemplateId))
            {
                continue;
            }

            var selection = new PromptTemplateSelectionEntry
            {
                TemplateId = item.TemplateId.Trim(),
            };

            if (!string.IsNullOrWhiteSpace(item.PromptParameters))
            {
                var parameters = ParseAndValidateParameters(item.PromptParameters);

                if (parameters != null)
                {
                    var template = await promptTemplateSelectionService.GetAsync(selection.TemplateId);

                    if (template == null)
                    {
                        modelState.AddModelError(
                            BuildFieldName(prefix, index, nameof(PromptTemplateSelectionItemViewModel.TemplateId)),
                            $"Prompt template '{selection.TemplateId}' was not found.");
                    }
                    else
                    {
                        var invalidKeys = GetInvalidParameterKeys(parameters, template);

                        if (invalidKeys.Count > 0)
                        {
                            modelState.AddModelError(
                                BuildFieldName(prefix, index, nameof(PromptTemplateSelectionItemViewModel.PromptParameters)),
                                $"The following parameter keys are not supported by this template: {string.Join(", ", invalidKeys)}");
                        }
                        else
                        {
                            selection.Parameters = parameters;
                        }
                    }
                }
                else
                {
                    modelState.AddModelError(
                        BuildFieldName(prefix, index, nameof(PromptTemplateSelectionItemViewModel.PromptParameters)),
                        "The parameters must be valid JSON with string key-value pairs. Example: {\"key1\": \"value1\"}");
                }
            }

            selections.Add(selection);
        }

        metadata.SetSelections(selections);

        return metadata;
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
        AITemplate template)
    {
        var invalidKeys = new List<string>();

        if (template?.Metadata.Parameters is not { Count: > 0 })
        {
            return [.. providedParameters.Keys];
        }

        var declaredNames = new HashSet<string>(
            template.Metadata.Parameters.Select(parameter => parameter.Name),
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

    private static string BuildFieldName(string prefix, int index, string propertyName)
        => $"{prefix}.{nameof(AITemplateSelectionViewModel.PromptTemplates)}[{index}].{propertyName}";
}
