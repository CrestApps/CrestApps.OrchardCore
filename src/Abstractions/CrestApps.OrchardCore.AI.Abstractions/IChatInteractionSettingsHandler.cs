using System.Text.Json;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Handles lifecycle events raised while saving <see cref="ChatInteraction"/> settings
/// from the client (e.g., SignalR hub).
/// </summary>
/// <remarks>
/// Implementations can enrich, validate, or otherwise mutate the <see cref="ChatInteraction"/>
/// based on the raw settings payload. The hub invokes <see cref="UpdatingAsync"/> before
/// core properties are persisted, and <see cref="UpdatedAsync"/> after the interaction is saved.
/// </remarks>
public interface IChatInteractionSettingsHandler
{
    /// <summary>
    /// Called while the <see cref="ChatInteraction"/> settings are being applied,
    /// before the interaction is persisted.
    /// </summary>
    /// <param name="interaction">The <see cref="ChatInteraction"/> being updated.</param>
    /// <param name="settings">The raw settings payload from the client.</param>
    Task UpdatingAsync(ChatInteraction interaction, JsonElement settings);

    /// <summary>
    /// Called after the <see cref="ChatInteraction"/> has been persisted.
    /// </summary>
    /// <param name="interaction">The updated <see cref="ChatInteraction"/>.</param>
    /// <param name="settings">The raw settings payload from the client.</param>
    Task UpdatedAsync(ChatInteraction interaction, JsonElement settings);
}
