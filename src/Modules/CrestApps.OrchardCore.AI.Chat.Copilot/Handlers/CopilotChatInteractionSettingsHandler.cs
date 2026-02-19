using System.Text.Json;
using CrestApps.OrchardCore.AI.Chat.Copilot.Models;
using CrestApps.OrchardCore.AI.Chat.Copilot.Services;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Handlers;

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
        var copilotFlags = GetString(settings, "copilotFlags");

        interaction.Put(new CopilotSessionMetadata
        {
            CopilotModel = copilotModel,
            CopilotFlags = copilotFlags,
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
}
