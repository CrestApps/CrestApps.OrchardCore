using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentTypes;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

/// <summary>
/// Attaches a part to a content type, or changes how an attached part is configured, with the content definition
/// service the content type editor of the admin uses.
/// </summary>
public sealed class SaveContentTypePartTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveContentTypePart";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentType": {
          "type": "string",
          "description": "The technical name of the content type, for example BlogPost."
        },
        "part": {
          "type": "string",
          "description": "The name of the content part, for example AutoroutePart or BagPart."
        },
        "name": {
          "type": "string",
          "description": "Only for a named part (a reusable part such as BagPart attached under its own name): the technical name of the part on this content type, for example Gallery. Leave it out for a normal part."
        },
        "displayName": {
          "type": "string",
          "description": "Only for a named part: its display name on this content type. Required when a named part is attached."
        },
        "description": {
          "type": "string",
          "description": "The description of the part on this content type."
        },
        "editor": {
          "type": "string",
          "description": "The name of the editor to use for the part, as the part's editor list in the admin shows it. An empty string selects the default editor."
        },
        "displayMode": {
          "type": "string",
          "description": "The name of the display mode to use for the part. An empty string selects the default display mode."
        },
        "settings": {
          "type": "object",
          "description": "The part's settings for this content type, as a JSON object keyed by settings class name, the shape a content definition recipe uses. Example: {\"AutoroutePartSettings\": {\"Pattern\": \"{{ ContentItem | display_text | slugify }}\", \"AllowCustomPath\": true}}. The given values are merged into the stored settings. They are not checked by the settings editor, so use setting names and values you know are valid."
        }
      },
      "required": [
        "contentType",
        "part"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Attaches a part to a content type or updates how it is attached: display name and description (named parts), editor, display mode and the part's settings for this type. Use it to add or reconfigure a single part without resending the whole type definition. Only the arguments given are changed. To change the order of the parts, use reorderContentTypeElements.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.EditContentTypes))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("contentType", out var contentType))
        {
            return "The contentType argument is required.";
        }

        if (!arguments.TryGetFirstString("part", out var partName))
        {
            return "The part argument is required.";
        }

        if (!ContentDefinitionToolArguments.TryReadSettings(arguments, out var settings))
        {
            return "Nothing was changed: settings must be a JSON object whose properties are settings objects, for example {\"AutoroutePartSettings\": {\"AllowCustomPath\": true}}.";
        }

        var contentDefinitionManager = services.GetRequiredService<IContentDefinitionManager>();
        var contentDefinitionService = services.GetRequiredService<IContentDefinitionService>();
        var typeDefinition = await contentDefinitionManager.LoadTypeDefinitionAsync(contentType);

        if (typeDefinition is null)
        {
            return $"Nothing was changed: there is no content type named '{contentType}'.";
        }

        var hasName = arguments.TryGetFirstString("name", out var name) && !string.Equals(name, partName, StringComparison.OrdinalIgnoreCase);
        var hasDisplayName = arguments.TryGetFirstString("displayName", out var displayName);
        var hasDescription = arguments.TryGetFirstString("description", true, out var description);
        var hasEditor = arguments.TryGetFirstString("editor", true, out var editor);
        var hasDisplayMode = arguments.TryGetFirstString("displayMode", true, out var displayMode);

        displayName = displayName?.Trim();

        var typePartName = hasName ? name.Trim() : partName.Trim();
        var typePart = FindPart(typeDefinition, typePartName);
        var attached = false;

        if (typePart is null)
        {
            var partDefinition = await contentDefinitionManager.LoadPartDefinitionAsync(partName.Trim());

            if (partDefinition is null)
            {
                return $"Nothing was changed: there is no content part named '{partName}'. Use listContentPartsDefinitions to see the parts.";
            }

            if (hasName)
            {
                if (!partDefinition.IsReusable())
                {
                    return $"Nothing was changed: the part '{partDefinition.Name}' is not reusable, so it cannot be attached under another name. Leave the name argument out.";
                }

                var nameError = ContentDefinitionToolArguments.ValidateTechnicalName(typePartName, true);

                if (nameError is not null)
                {
                    return $"Nothing was changed: {nameError}";
                }

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    return "Nothing was changed: a named part needs a displayName.";
                }

                if (HasPartWithDisplayName(typeDefinition, displayName, null))
                {
                    return $"Nothing was changed: the content type '{typeDefinition.Name}' already has a part with the display name '{displayName}'.";
                }

                await contentDefinitionService.AddReusablePartToTypeAsync(typePartName, displayName, description, partDefinition.Name, typeDefinition.Name);
            }
            else
            {
                if (!partDefinition.IsAttachable())
                {
                    return $"Nothing was changed: the part '{partDefinition.Name}' is not attachable to content types.";
                }

                if (hasDisplayName)
                {
                    return "Nothing was changed: displayName applies to named parts only. Give the name argument to attach a reusable part under its own name.";
                }

                await contentDefinitionService.AddPartToTypeAsync(partDefinition.Name, typeDefinition.Name);
            }

            attached = true;
            typeDefinition = await contentDefinitionManager.LoadTypeDefinitionAsync(typeDefinition.Name);
            typePart = FindPart(typeDefinition, typePartName);

            if (typePart is null)
            {
                return $"The part '{typePartName}' could not be attached to the content type '{typeDefinition.Name}'.";
            }
        }
        else
        {
            if (!string.Equals(typePart.PartDefinition.Name, partName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return $"Nothing was changed: the part '{typePart.Name}' on the content type '{typeDefinition.Name}' is a '{typePart.PartDefinition.Name}', not a '{partName}'.";
            }

            if (hasDisplayName)
            {
                // As in the editor, only a reusable part has its own display name on a content type.
                if (!typePart.PartDefinition.IsReusable())
                {
                    return "Nothing was changed: displayName applies to named (reusable) parts only.";
                }

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    return "Nothing was changed: the displayName can't be empty.";
                }

                if (HasPartWithDisplayName(typeDefinition, displayName, typePart.Name))
                {
                    return $"Nothing was changed: the content type '{typeDefinition.Name}' already has a part with the display name '{displayName}'.";
                }
            }
        }

        // A named part that was just attached already has its display name and description.
        var altersPart = hasEditor || hasDisplayMode || ((hasDisplayName || hasDescription) && !(attached && hasName));

        if (altersPart)
        {
            // The service writes every value it is given, so the values that are not changed are sent back as they are.
            await contentDefinitionService.AlterTypePartAsync(new AlterTypePartContext
            {
                TypeName = typeDefinition.Name,
                PartDefinition = typePart.PartDefinition,
                PartName = typePart.Name,
                DisplayName = hasDisplayName ? displayName : typePart.DisplayName(),
                Description = hasDescription ? description : typePart.Description(),
                Editor = hasEditor ? editor : typePart.Editor(),
                DisplayMode = hasDisplayMode ? displayMode : typePart.DisplayMode(),
            });
        }

        if (settings is { Count: > 0 })
        {
            var partDefinition = typePart.PartDefinition;

            await contentDefinitionManager.AlterTypeDefinitionAsync(typeDefinition.Name, type => type.WithPart(typePart.Name, partDefinition, part => part.MergeSettings(settings)));
        }

        if (!attached && !altersPart && settings is not { Count: > 0 })
        {
            return $"Nothing was changed: the part '{typePart.Name}' is already attached to the content type '{typeDefinition.Name}' and no value to change was given.";
        }

        typeDefinition = await contentDefinitionManager.LoadTypeDefinitionAsync(typeDefinition.Name);
        typePart = FindPart(typeDefinition, typePartName);

        return AgentToolResults.Json(new
        {
            saved = true,
            attached,
            contentType = typeDefinition.Name,
            part = new
            {
                name = typePart.Name,
                part = typePart.PartDefinition.Name,
                displayName = typePart.DisplayName(),
                description = typePart.Description(),
                editor = typePart.Editor(),
                displayMode = typePart.DisplayMode(),
                settings = typePart.Settings,
            },
        });
    }

    private static ContentTypePartDefinition FindPart(ContentTypeDefinition typeDefinition, string name)
        => typeDefinition.Parts.FirstOrDefault(part => string.Equals(part.Name, name, StringComparison.OrdinalIgnoreCase));

    private static bool HasPartWithDisplayName(ContentTypeDefinition typeDefinition, string displayName, string exceptPartName)
    {
        return typeDefinition.Parts.Any(part =>
            part.Name != exceptPartName &&
            string.Equals(part.DisplayName()?.Trim(), displayName, StringComparison.OrdinalIgnoreCase));
    }
}
