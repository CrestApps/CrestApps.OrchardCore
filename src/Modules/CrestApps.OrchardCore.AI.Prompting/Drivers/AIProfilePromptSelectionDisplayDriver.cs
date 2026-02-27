using System.Text.Json;
using CrestApps.AI.Prompting.Services;
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
            model.SelectedPromptId = profile.Properties["PromptTemplateId"]?.ToString();
            model.PromptParameters = profile.Properties["PromptParameters"]?.ToString();

            PopulateViewModel(model, listableTemplates);
        }).Location("Content:9");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new AITemplateSelectionViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!string.IsNullOrEmpty(model.SelectedPromptId))
        {
            profile.Properties["PromptTemplateId"] = model.SelectedPromptId;

            if (!string.IsNullOrEmpty(model.PromptParameters))
            {
                if (IsValidJsonKeyValuePair(model.PromptParameters))
                {
                    profile.Properties["PromptParameters"] = model.PromptParameters;
                }
                else
                {
                    context.Updater.ModelState.AddModelError(
                        Prefix + '.' + nameof(model.PromptParameters),
                        "The parameters must be valid JSON with string key-value pairs. Example: {\"key1\": \"value1\"}");
                }
            }
            else
            {
                profile.Properties.Remove("PromptParameters");
            }
        }
        else
        {
            profile.Properties.Remove("PromptTemplateId");
            profile.Properties.Remove("PromptParameters");
        }

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
        }

        model.AvailableGroups = groups.Values.ToList();
    }

    private static bool IsValidJsonKeyValuePair(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
