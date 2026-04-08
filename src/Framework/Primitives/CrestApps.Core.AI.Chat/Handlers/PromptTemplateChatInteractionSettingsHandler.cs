using System.Text.Json;
using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Chat.Handlers;

public sealed class PromptTemplateChatInteractionSettingsHandler : IChatInteractionSettingsHandler
{
    public Task UpdatingAsync(ChatInteraction interaction, JsonElement settings)
    {
        interaction.Alter<PromptTemplateMetadata>(metadata =>
        {
            metadata.SetSelections(GetSelections(settings));
        });

        return Task.CompletedTask;
    }

    public Task UpdatedAsync(ChatInteraction interaction, JsonElement settings)
        => Task.CompletedTask;

    private static List<PromptTemplateSelectionEntry> GetSelections(JsonElement settings)
    {
        if (!settings.TryGetProperty("promptTemplates", out var promptTemplates) || promptTemplates.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var selections = new List<PromptTemplateSelectionEntry>();

        foreach (var promptTemplate in promptTemplates.EnumerateArray())
        {
            var templateId = GetString(promptTemplate, "templateId");

            if (string.IsNullOrWhiteSpace(templateId))
            {
                continue;
            }

            var selection = new PromptTemplateSelectionEntry
            {
                TemplateId = templateId,
                Parameters = ParseParameters(GetString(promptTemplate, "promptParameters")),
            };

            selections.Add(selection);
        }

        return selections;
    }

    private static Dictionary<string, object> ParseParameters(string promptParameters)
    {
        if (string.IsNullOrWhiteSpace(promptParameters))
        {
            return null;
        }

        using var document = JsonDocument.Parse(promptParameters);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                parameters[property.Name] = property.Value.GetString();
            }
        }

        return parameters.Count > 0 ? parameters : null;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        return null;
    }
}
