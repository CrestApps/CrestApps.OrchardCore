using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Agent.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

/// <summary>
/// Adds a field to a content part, or changes an existing field, with the content definition service the content
/// definition editor of the admin uses.
/// </summary>
public sealed class SaveContentPartFieldTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveContentPartField";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "part": {
          "type": "string",
          "description": "The name of the content part that holds the field. For a field of a content type itself, give the technical name of the content type, for example BlogPost."
        },
        "name": {
          "type": "string",
          "description": "The technical name of the field. Required to update a field. For a new field, leave it out to have it made from the displayName."
        },
        "displayName": {
          "type": "string",
          "description": "The display name of the field. Required for a new field."
        },
        "fieldType": {
          "type": "string",
          "description": "The type of a new field, for example TextField. Use listContentFieldDefinitions to see the field types. It cannot be changed after creation."
        },
        "editor": {
          "type": "string",
          "description": "The name of the editor to use for the field, as the field's editor list in the admin shows it. An empty string selects the default editor."
        },
        "displayMode": {
          "type": "string",
          "description": "The name of the display mode to use for the field. An empty string selects the default display mode."
        },
        "settings": {
          "type": "object",
          "description": "The field's settings, as a JSON object keyed by settings class name, the shape a content definition recipe uses. Example: {\"TextFieldSettings\": {\"Required\": true, \"Hint\": \"Shown under the title.\"}}. The given values are merged into the stored settings. They are not checked by the settings editor, so use setting names and values you know are valid."
        }
      },
      "required": [
        "part"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Adds a field to a content part (or to a content type's own part) or updates an existing field: display name, field type on creation, editor, display mode and the field's settings. The field type cannot be changed after creation. Only the arguments given are changed. To change the order of the fields, use reorderContentTypeElements.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.EditContentTypes))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("part", out var part))
        {
            return "The part argument is required.";
        }

        if (!ContentDefinitionToolArguments.TryReadSettings(arguments, out var settings))
        {
            return "Nothing was changed: settings must be a JSON object whose properties are settings objects, for example {\"TextFieldSettings\": {\"Required\": true}}.";
        }

        var contentDefinitionManager = services.GetRequiredService<IContentDefinitionManager>();
        var contentDefinitionService = services.GetRequiredService<IContentDefinitionService>();
        var partDefinition = await contentDefinitionManager.LoadPartDefinitionAsync(part.Trim());
        string partName;

        if (partDefinition is not null)
        {
            partName = partDefinition.Name;
        }
        else
        {
            // A content type that has no field yet has no part of its own; the service creates it with the first field.
            var typeDefinition = await contentDefinitionManager.LoadTypeDefinitionAsync(part.Trim());

            if (typeDefinition is null)
            {
                return $"Nothing was changed: there is no content part or content type named '{part}'.";
            }

            partName = typeDefinition.Name;
        }

        var hasName = arguments.TryGetFirstString("name", out var name);
        var hasDisplayName = arguments.TryGetFirstString("displayName", out var displayName);
        var hasFieldType = arguments.TryGetFirstString("fieldType", out var fieldType);
        var hasEditor = arguments.TryGetFirstString("editor", true, out var editor);
        var hasDisplayMode = arguments.TryGetFirstString("displayMode", true, out var displayMode);

        name = name?.Trim();
        displayName = displayName?.Trim();

        var fields = partDefinition?.Fields ?? [];
        var field = hasName
            ? fields.FirstOrDefault(existing => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase))
            : null;
        var added = false;

        if (field is null)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return hasName
                    ? $"Nothing was changed: the part '{partName}' has no field named '{name}'. To add it, give a displayName and a fieldType."
                    : "Nothing was changed: give the name of the field to update, or a displayName and a fieldType to add a field.";
            }

            if (!hasFieldType)
            {
                return "Nothing was changed: a new field needs a fieldType, for example TextField.";
            }

            var fieldTypes = await services.GetRequiredService<ContentMetadataService>().GetFieldsAsync();
            var fieldTypeName = fieldTypes
                .Select(type => type.Name)
                .FirstOrDefault(typeName => string.Equals(typeName, fieldType.Trim(), StringComparison.OrdinalIgnoreCase));

            if (fieldTypeName is null)
            {
                return $"Nothing was changed: there is no field type named '{fieldType}'. Use listContentFieldDefinitions to see the field types.";
            }

            if (HasFieldWithDisplayName(fields, displayName, null))
            {
                return $"Nothing was changed: the part '{partName}' already has a field with the display name '{displayName}'.";
            }

            if (!hasName)
            {
                name = await contentDefinitionService.GenerateFieldNameFromDisplayNameAsync(partName, displayName);
            }

            var nameError = ContentDefinitionToolArguments.ValidateTechnicalName(name, false);

            if (nameError is not null)
            {
                return $"Nothing was changed: {nameError}";
            }

            await contentDefinitionService.AddFieldToPartAsync(name, displayName, fieldTypeName, partName);

            added = true;
            field = await FindFieldAsync(contentDefinitionManager, partName, name);

            if (field is null)
            {
                return $"The field '{name}' could not be added to the part '{partName}'.";
            }
        }
        else
        {
            if (hasFieldType && !string.Equals(field.FieldDefinition.Name, fieldType.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return $"Nothing was changed: the field '{field.Name}' is a '{field.FieldDefinition.Name}' and the field type cannot be changed after creation.";
            }

            if (hasDisplayName && HasFieldWithDisplayName(fields, displayName, field.Name))
            {
                return $"Nothing was changed: the part '{partName}' already has a field with the display name '{displayName}'.";
            }
        }

        var altersField = hasEditor || hasDisplayMode || (hasDisplayName && !added);

        if (altersField)
        {
            // The service writes every value it is given, so the values that are not changed are sent back as they are.
            await contentDefinitionService.AlterFieldAsync(new AlterFieldContext
            {
                PartName = partName,
                FieldName = field.Name,
                DisplayName = hasDisplayName ? displayName : field.DisplayName(),
                Editor = hasEditor ? editor : field.Editor(),
                DisplayMode = hasDisplayMode ? displayMode : field.DisplayMode(),
            });
        }

        if (settings is { Count: > 0 })
        {
            var fieldName = field.Name;

            await contentDefinitionManager.AlterPartDefinitionAsync(partName, partBuilder => partBuilder.WithField(fieldName, fieldBuilder => fieldBuilder.MergeSettings(settings)));
        }

        if (!added && !altersField && settings is not { Count: > 0 })
        {
            return $"Nothing was changed: no value to change was given for the field '{field.Name}'.";
        }

        field = await FindFieldAsync(contentDefinitionManager, partName, field.Name);

        return AgentToolResults.Json(new
        {
            saved = true,
            added,
            part = partName,
            field = new
            {
                name = field.Name,
                fieldType = field.FieldDefinition.Name,
                displayName = field.DisplayName(),
                editor = field.Editor(),
                displayMode = field.DisplayMode(),
                settings = field.Settings,
            },
        });
    }

    private static async Task<ContentPartFieldDefinition> FindFieldAsync(IContentDefinitionManager contentDefinitionManager, string partName, string fieldName)
    {
        var partDefinition = await contentDefinitionManager.LoadPartDefinitionAsync(partName);

        return partDefinition?.Fields.FirstOrDefault(field => string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasFieldWithDisplayName(IEnumerable<ContentPartFieldDefinition> fields, string displayName, string exceptFieldName)
    {
        return fields.Any(field =>
            field.Name != exceptFieldName &&
            string.Equals(field.DisplayName()?.Trim(), displayName, StringComparison.OrdinalIgnoreCase));
    }
}
