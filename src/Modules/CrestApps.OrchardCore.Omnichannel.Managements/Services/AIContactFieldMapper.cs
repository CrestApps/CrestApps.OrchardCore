using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Writes a closed conversation's extracted fields to the fields of its contact, as the profile maps them: replacing
/// what the field held, filling it only when empty, or adding to a text field.
/// </summary>
public sealed class AIContactFieldMapper : IAIConversationClosedHandler
{
    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIContactFieldMapper"/> class.
    /// </summary>
    /// <param name="contentManager">Loads and saves the contact.</param>
    /// <param name="contentDefinitionManager">Tells the kind of each contact field.</param>
    /// <param name="logger">The logger.</param>
    public AIContactFieldMapper(IContentManager contentManager, IContentDefinitionManager contentDefinitionManager, ILogger<AIContactFieldMapper> logger)
    {
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ClosedAsync(AIProfile profile, AIChatSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(session);

        var mappings = profile.GetSettings<AIProfileContactFieldMappingSettings>().Mappings
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping.FieldName) && !string.IsNullOrWhiteSpace(mapping.ContactField))
            .ToList();

        if (mappings.Count == 0 ||
            !session.TryGet<AIConversationContactMetadata>(out var contactMetadata) ||
            string.IsNullOrEmpty(contactMetadata.ContactId))
        {
            return;
        }

        var contact = await _contentManager.GetAsync(contactMetadata.ContactId, VersionOptions.Latest);

        if (contact is null)
        {
            return;
        }

        var fieldTypes = await GetFieldTypesAsync(contact.ContentType);
        var changed = 0;

        foreach (var mapping in mappings)
        {
            var values = session.ExtractedData.TryGetValue(mapping.FieldName, out var field) ? field?.Values ?? [] : [];
            var value = string.Join("; ", values.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()));

            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (!fieldTypes.TryGetValue(mapping.ContactField.Trim(), out var fieldType))
            {
                _logger.LogWarning("The contact type '{ContentType}' has no field '{Field}' to map the extracted field '{Name}' to.", contact.ContentType, mapping.ContactField, mapping.FieldName);

                continue;
            }

            if (Apply(contact.Content, mapping.ContactField.Trim(), fieldType, value, mapping.Mode))
            {
                changed++;
            }
        }

        if (changed > 0)
        {
            await _contentManager.UpdateAsync(contact);
        }
    }

    /// <summary>
    /// Writes a value to a field of a content item's JSON.
    /// </summary>
    /// <param name="content">The content item's JSON.</param>
    /// <param name="path">The field, as <c>PartName.FieldName</c>.</param>
    /// <param name="fieldType">The field's type, such as <c>TextField</c>.</param>
    /// <param name="value">The value.</param>
    /// <param name="mode">How to write it.</param>
    /// <returns>Whether the field changed.</returns>
    public static bool Apply(JsonObject content, string path, string fieldType, string value, AIContactFieldMappingMode mode)
    {
        ArgumentNullException.ThrowIfNull(content);

        var parts = path.Split('.', 2);

        if (parts.Length != 2)
        {
            return false;
        }

        var (property, node) = Convert(fieldType, value);

        if (node is null)
        {
            return false;
        }

        var part = content[parts[0]] as JsonObject ?? [];
        var target = part[parts[1]] as JsonObject ?? [];
        var current = target[property];
        var isEmpty = current is null || (current is JsonValue text && text.TryGetValue<string>(out var currentText) && string.IsNullOrWhiteSpace(currentText));

        switch (mode)
        {
            case AIContactFieldMappingMode.FillIfEmpty when !isEmpty:
                return false;

            case AIContactFieldMappingMode.Append when !isEmpty && property == "Text":
                var existing = current.GetValue<string>();

                if (existing.Contains(value, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                node = JsonValue.Create($"{existing}; {value}");
                break;
        }

        if (JsonNode.DeepEquals(current, node))
        {
            return false;
        }

        target[property] = node;
        part[parts[1]] = target;
        content[parts[0]] = part;

        return true;
    }

    private static (string Property, JsonNode Node) Convert(string fieldType, string value)
        => fieldType switch
        {
            "TextField" or "HtmlField" or "MarkdownField" => (fieldType == "HtmlField" ? "Html" : fieldType == "MarkdownField" ? "Markdown" : "Text", JsonValue.Create(value)),
            "NumericField" => ("Value", decimal.TryParse(value.Split(';')[0].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? JsonValue.Create(number) : null),
            "BooleanField" => ("Value", ParseBoolean(value) is { } flag ? JsonValue.Create(flag) : null),
            "DateField" => ("Value", DateTime.TryParse(value.Split(';')[0].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? JsonValue.Create(date.Date) : null),
            "DateTimeField" => ("Value", DateTime.TryParse(value.Split(';')[0].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var moment) ? JsonValue.Create(moment) : null),
            _ => ("Text", JsonValue.Create(value)),
        };

    private static bool? ParseBoolean(string value)
        => value.Split(';')[0].Trim().ToLowerInvariant() switch
        {
            "true" or "yes" or "y" or "1" => true,
            "false" or "no" or "n" or "0" => false,
            _ => null,
        };

    private async Task<Dictionary<string, string>> GetFieldTypesAsync(string contentType)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

        foreach (var part in definition?.Parts ?? [])
        {
            foreach (var field in part.PartDefinition?.Fields ?? [])
            {
                fields[$"{part.Name}.{field.Name}"] = field.FieldDefinition?.Name;
            }
        }

        return fields;
    }
}
