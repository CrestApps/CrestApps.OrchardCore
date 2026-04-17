using CrestApps.Core.AI.Claude.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Claude.Services;

internal static class ClaudeModelSelectListFactory
{
    public static List<SelectListItem> Build(
        IEnumerable<ClaudeModelInfo> models,
        params string[] fallbackModelIds)
    {
        var items = (models ?? [])
            .Where(model => !string.IsNullOrWhiteSpace(model.Id))
            .Select(model => new SelectListItem(
                string.IsNullOrWhiteSpace(model.Name) ? model.Id : model.Name,
                model.Id))
            .ToList();

        var knownIds = items
            .Select(item => item.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var modelId in fallbackModelIds ?? [])
        {
            if (string.IsNullOrWhiteSpace(modelId) || !knownIds.Add(modelId))
            {
                continue;
            }

            items.Add(new SelectListItem(modelId, modelId));
        }

        return items
            .OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
