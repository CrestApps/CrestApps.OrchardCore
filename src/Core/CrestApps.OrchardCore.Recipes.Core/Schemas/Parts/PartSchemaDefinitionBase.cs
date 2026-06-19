using System.Text.Json.Nodes;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

/// <summary>
/// Provides the standard implementation surface for content part settings schema definitions.
/// </summary>
/// <remarks>
/// Use this base class when a feature contributes JSON schema for a content part's
/// <c>Settings</c> object inside the <c>ContentDefinition</c> or
/// <c>ReplaceContentDefinition</c> recipe steps. Implementations only need to supply the
/// part name and the part-specific schema fragments; this base class handles the
/// <see cref="ContentDefinitionSchemaType.Part"/> classification and schema caching.
/// </remarks>
public abstract class PartSchemaDefinitionBase : IContentSchemaDefinition, IContentPartSchemaDefinition
{
    private JsonSchemaBuilder _cachedSchema;

    /// <summary>
    /// Gets the schema definition category used when composing content definition steps.
    /// </summary>
    public ContentDefinitionSchemaType Type { get; } = ContentDefinitionSchemaType.Part;

    /// <summary>
    /// Gets the Orchard Core content part name that this schema contributes settings for.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the JSON schema fragment for the part settings envelope.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public ValueTask<JsonSchemaBuilder> GetSettingsSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cachedSchema ??= BuildSettingsCore();

        return ValueTask.FromResult(_cachedSchema);
    }

    ValueTask<JsonSchemaBuilder> IContentPartSchemaDefinition.GetPartSchemaAsync(
        ContentPartSchemaContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return BuildPartSchemaAsync(context, cancellationToken);
    }

    /// <summary>
    /// Builds the part-specific settings schema.
    /// </summary>
    /// <remarks>
    /// Return the full fragment that will be merged into the part <c>Settings</c> object.
    /// </remarks>
    protected abstract JsonSchemaBuilder BuildSettingsCore();

    /// <summary>
    /// Builds the content item payload schema for the content part.
    /// Override this method when the schema requires asynchronous work.
    /// </summary>
    /// <param name="context">The context describing the concrete part attachment.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    protected virtual ValueTask<JsonSchemaBuilder> BuildPartSchemaAsync(
        ContentPartSchemaContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(BuildPartSchemaCore());

    /// <summary>
    /// Builds the content item payload schema for the content part.
    /// </summary>
    protected virtual JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true);

    /// <summary>
    /// Reads a string-array settings value from the supplied part settings object.
    /// </summary>
    /// <param name="settings">The settings object to inspect.</param>
    /// <param name="settingsName">The nested settings object name.</param>
    /// <param name="propertyName">The string-array property name.</param>
    protected static string[] GetStringSettings(JsonObject settings, string settingsName, string propertyName)
    {
        if (settings is null ||
            !settings.TryGetPropertyValue(settingsName, out var settingsNode) ||
            settingsNode is not JsonObject settingsObject ||
            !settingsObject.TryGetPropertyValue(propertyName, out var propertyNode) ||
            propertyNode is not JsonArray propertyArray)
        {
            return [];
        }

        return propertyArray
            .Select(node => node?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }
}
