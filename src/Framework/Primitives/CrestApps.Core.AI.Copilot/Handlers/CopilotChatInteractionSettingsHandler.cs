using System.Text.Json;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Copilot.Models;
using CrestApps.Core.AI.Copilot.Services;
using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Copilot.Handlers;

/// <summary>
/// Handles Copilot-specific settings (model and flags) when a
/// <see cref="ChatInteraction"/> is saved via the SignalR hub.
/// </summary>
internal sealed class CopilotChatInteractionSettingsHandler : IChatInteractionSettingsHandler
{
    public Task UpdatingAsync(ChatInteraction interaction, JsonElement settings)
    {
        var orchestratorName = GetString(settings, "orchestratorName") ?? interaction.OrchestratorName;

        if (!string.Equals(orchestratorName, CopilotOrchestrator.OrchestratorName, StringComparison.OrdinalIgnoreCase))
        {
            interaction.Remove<CopilotSessionMetadata>();
            return Task.CompletedTask;
        }

        var copilotModel = GetString(settings, "copilotModel");
        var isAllowAll = GetBool(settings, "isAllowAll");

        interaction.Alter<CopilotSessionMetadata>(metadata =>
        {
            metadata.CopilotModel = copilotModel;
            metadata.IsAllowAll = isAllowAll;
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
