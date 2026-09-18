using System.Globalization;
using System.Text.Json;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Documents.Models;
using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Documents.Handlers;

/// <summary>
/// Carries the indexable-character ceiling from a chat interaction's settings payload onto its metadata.
/// </summary>
/// <remarks>
/// A chat interaction has no form POST: its editor is saved through the SignalR hub, which hands each
/// tagged <c>setting-input</c> to the registered settings handlers as a raw JSON payload. The framework's
/// own handler carries the retrieval mode across; this one arrived later and needs the same treatment, or
/// the field saves nothing and reads back empty.
/// <para>
/// Only the ceiling. The framework's interaction upload endpoint follows the site's own answer about
/// describing figures whatever an interaction says, so that choice belongs on the AI profile, where it
/// applies to every session the profile serves.
/// </para>
/// </remarks>
internal sealed class ChatInteractionUploadCeilingSettingsHandler : IChatInteractionSettingsHandler
{
    /// <summary>
    /// Applies the indexable-character ceiling from the raw client payload to the interaction metadata.
    /// </summary>
    /// <param name="interaction">The chat interaction being updated.</param>
    /// <param name="settings">The raw settings payload from the client.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task UpdatingAsync(ChatInteraction interaction, JsonElement settings, CancellationToken cancellationToken = default)
    {
        interaction.Alter<DocumentsMetadata>(metadata =>
        {
            metadata.MaxIndexableCharacters = GetCharacterCeiling(settings, "maxIndexableCharacters");
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs after the interaction has been persisted.
    /// </summary>
    /// <param name="interaction">The updated chat interaction.</param>
    /// <param name="settings">The raw settings payload from the client.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task UpdatedAsync(ChatInteraction interaction, JsonElement settings, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Reads a character ceiling, treating a missing or blank value as "use the site default" and clamping
    /// a negative number to the "no limit" zero.
    /// </summary>
    private static int? GetCharacterCeiling(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return Math.Max(0, number);
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            var value = property.GetString();

            if (!string.IsNullOrWhiteSpace(value) &&
                int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return Math.Max(0, parsed);
            }
        }

        return null;
    }

}
