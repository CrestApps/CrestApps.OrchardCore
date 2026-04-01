using System.Text.Json;
using CrestApps.AI.Chat;
using CrestApps.AI.Copilot.Models;
using CrestApps.AI.Copilot.Services;
using CrestApps.AI.Models;

namespace CrestApps.AI.Copilot.Handlers;

/// <summary>
/// Handles Copilot-specific settings (model and flags) when a
/// <see cref="ChatInteraction"/> is saved via the SignalR hub.
/// </summary>
internal sealed class CopilotChatInteractionSettingsHandler : IChatInteractionSettingsHandler
{
    public Task UpdatingAsync(ChatInteraction interaction, JsonElement settings)
    {
        if (!string.Equals(interaction.OrchestratorName, CopilotOrchestrator.OrchestratorName, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var copilotModel = GetString(settings, "copilotModel");
        var isAllowAll = GetBool(settings, "isAllowAll");
        interaction.Put(new CopilotSessionMetadata
        {
            CopilotModel = copilotModel,
            IsAllowAll = isAllowAll,
        });

        return Task.CompletedTask;
    }

    public Task UpdatedAsync(ChatInteraction interaction, JsonElement settings)
        => Task.CompletedTask;
    private static string GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private static bool GetBool(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                return string.Equals(prop.GetString(), "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }
}
