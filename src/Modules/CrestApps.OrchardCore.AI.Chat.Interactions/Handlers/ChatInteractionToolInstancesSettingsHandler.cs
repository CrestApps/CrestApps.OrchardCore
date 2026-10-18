using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Tools.Services;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Handlers;

/// <summary>
/// Persists the tool instances selected on a chat interaction when the chat page saves its settings over
/// SignalR. The chat page never posts the editor form, so without this handler the selection is dropped and
/// the model never sees the selected instances.
/// </summary>
internal sealed class ChatInteractionToolInstancesSettingsHandler : IChatInteractionSettingsHandler
{
    private const string ToolInstanceNamesKey = "toolInstanceNames";

    private readonly IAIToolInstanceAccessor _instanceAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatInteractionToolInstancesSettingsHandler"/> class.
    /// </summary>
    /// <param name="instanceAccessor">The accessor used to resolve the instances the current user may assign.</param>
    public ChatInteractionToolInstancesSettingsHandler(IAIToolInstanceAccessor instanceAccessor)
    {
        _instanceAccessor = instanceAccessor;
    }

    /// <summary>
    /// Stores the selected tool instance names, keeping only the instances the current user may assign.
    /// A payload without a selection leaves the stored one untouched, so a client that does not send it
    /// cannot clear it.
    /// </summary>
    /// <param name="interaction">The interaction being updated.</param>
    /// <param name="settings">The raw settings payload from the client.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task UpdatingAsync(ChatInteraction interaction, JsonElement settings, CancellationToken cancellationToken = default)
    {
        if (settings.ValueKind != JsonValueKind.Object ||
            !settings.TryGetProperty(ToolInstanceNamesKey, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var selectedNames = property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var accessibleInstances = await _instanceAccessor.GetAccessibleInstancesAsync(cancellationToken);

        var instanceNames = accessibleInstances
            .Select(instance => instance.Name)
            .Where(selectedNames.Contains)
            .ToArray();

        interaction.Alter<AIToolInstanceMetadata>(metadata => metadata.ToolInstanceNames = instanceNames);
    }

    /// <summary>
    /// Nothing to do once the interaction is saved.
    /// </summary>
    /// <param name="interaction">The updated interaction.</param>
    /// <param name="settings">The raw settings payload from the client.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task UpdatedAsync(ChatInteraction interaction, JsonElement settings, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
