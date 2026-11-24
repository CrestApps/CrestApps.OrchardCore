using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Agent.Roles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

/// <summary>
/// Lists the template names (shape alternates) a content type's items are rendered with in a display type, so a
/// template is given a name Orchard Core actually looks for. The part and field alternates come from Orchard Core's own
/// alternate factories (<see cref="ContentPartAlternatesFactory"/> and <see cref="ContentFieldAlternatesFactory"/>), for
/// the parts and fields that have a display driver; the content item alternates, whose factories are internal to
/// Orchard Core, follow its documented content item conventions.
/// </summary>
public sealed class ListShapeAlternatesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listShapeAlternates";

    // The most shapes one result lists; a type rarely has more parts and fields.
    private const int MaxShapes = 60;

    // How a display driver names the shape of a part or a field shown with a display mode.
    private const string DisplayModeSeparator = "_Display__";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentType": {
          "type": "string",
          "description": "The technical name of the content type, for example BlogPost."
        },
        "displayType": {
          "type": "string",
          "description": "The display type the items are shown with: Detail (the item's own page, the default), Summary (in lists), or another display type a theme uses, such as SummaryAdmin."
        }
      },
      "required": [
        "contentType"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the template names Orchard Core looks for when it shows items of a content type in a display type (Detail or Summary): the content item shape and the shapes of each part and field, each with its shape name (for the Templates admin page) and its theme file name. Use it before naming a template, so the name is one Orchard Core actually uses; the last matching name with a template wins.";

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

        // The names reveal the type's parts and fields, which the content definition pages show to this permission.
        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.ViewContentTypes))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("contentType", out var contentType) || string.IsNullOrWhiteSpace(contentType))
        {
            return "Give contentType, the technical name of a content type. Use listContentTypesDefinitions to find it.";
        }

        contentType = contentType.Trim();

        var definitionManager = services.GetRequiredService<IContentDefinitionManager>();
        var definition = await definitionManager.GetTypeDefinitionAsync(contentType);

        if (definition is null)
        {
            var close = RoleToolSupport.FindCloseNames(contentType, (await definitionManager.ListTypeDefinitionsAsync()).Select(type => type.Name));

            return $"There is no content type named '{contentType}'.{(close.Count > 0 ? $" Did you mean {string.Join(", ", close)}?" : string.Empty)} Use listContentTypesDefinitions to find it.";
        }

        arguments.TryGetFirstString("displayType", out var displayType);
        displayType = string.IsNullOrWhiteSpace(displayType) ? OrchardCoreConstants.DisplayType.Detail : displayType.Trim();

        var partDrivers = services.GetService<IContentPartDisplayDriverResolver>();
        var fieldDrivers = services.GetService<IContentFieldDisplayDriverResolver>();
        var shapes = new List<object> { DescribeContentItem(definition, displayType) };
        var skippedParts = new List<string>();
        var truncated = false;

        foreach (var typePart in definition.Parts)
        {
            if (shapes.Count >= MaxShapes)
            {
                truncated = true;

                break;
            }

            var partType = typePart.PartDefinition.Name;
            var partDisplayMode = typePart.DisplayMode();

            if (partDrivers is not null && partDrivers.GetDisplayModeDrivers(partType, partDisplayMode).Count > 0)
            {
                var shapeType = string.IsNullOrEmpty(partDisplayMode) ? partType : partType + DisplayModeSeparator + partDisplayMode;

                shapes.Add(new
                {
                    kind = "part",
                    part = typePart.Name,
                    partType,
                    shape = shapeType,
                    alternates = Describe(ContentPartAlternatesFactory.GetAlternates(typePart, shapeType, displayType), displayType),
                });
            }
            else if (!string.Equals(partType, definition.Name, StringComparison.Ordinal))
            {
                // A part made only of fields, attached under its own name, is shown in a ContentPart shape that is not
                // listed here; its fields are.
                skippedParts.Add(typePart.Name);
            }

            foreach (var partField in typePart.PartDefinition.Fields)
            {
                if (shapes.Count >= MaxShapes)
                {
                    truncated = true;

                    break;
                }

                var fieldType = partField.FieldDefinition.Name;
                var fieldDisplayMode = partField.DisplayMode();

                if (fieldDrivers is null || fieldDrivers.GetDisplayModeDrivers(fieldType, fieldDisplayMode).Count == 0)
                {
                    continue;
                }

                var shapeType = string.IsNullOrEmpty(fieldDisplayMode) ? fieldType : fieldType + DisplayModeSeparator + fieldDisplayMode;

                shapes.Add(new
                {
                    kind = "field",
                    part = typePart.Name,
                    field = partField.Name,
                    fieldType,
                    shape = shapeType,
                    alternates = Describe(ContentFieldAlternatesFactory.GetAlternates(typePart, partField, shapeType, displayType), displayType),
                });
            }
        }

        var notes = new List<string>
        {
            "For each shape, Orchard Core renders the last alternate that has a template, or the shape itself. Give the name to a template on the Templates admin page, or create the file (with .cshtml or .liquid) in the theme's Views folder.",
            "A single item also has the alternates Content__[ContentItemId] and Content_" + displayType + "__[ContentItemId]. Some parts render more than their main shape (a list part also renders its feed, for example); those are not listed.",
        };

        if (skippedParts.Count > 0)
        {
            notes.Add($"The parts {string.Join(", ", skippedParts)} have no display driver of their own: only their fields are listed.");
        }

        return AgentToolResults.Json(new
        {
            contentType = definition.Name,
            displayType,
            stereotype = definition.GetStereotype(),
            shapes,
            truncated,
            note = string.Join(" ", notes),
        });
    }

    // The content item shape, as the content item display manager and the Contents module name it. Their alternate
    // factories are internal, so the documented conventions are built here: the shape is Content (or the type's
    // stereotype, such as Widget), followed by _[DisplayType] for any display type but Detail.
    private static object DescribeContentItem(ContentTypeDefinition definition, string displayType)
    {
        var isDetail = string.Equals(displayType, OrchardCoreConstants.DisplayType.Detail, StringComparison.Ordinal);
        var stereotype = definition.GetStereotype();
        var hasStereotype = !string.IsNullOrEmpty(stereotype);
        var baseShape = hasStereotype ? stereotype : "Content";
        var shapeType = isDetail ? baseShape : baseShape + "_" + displayType;
        var alternates = new List<string>();

        if (hasStereotype)
        {
            // Stereotype_[DisplayType] and [Stereotype]_[DisplayType], e.g. Widget.Summary.
            alternates.Add(isDetail ? "Stereotype" : "Stereotype_" + displayType);
            alternates.Add(shapeType);
        }
        else
        {
            // Content_[DisplayType], e.g. Content.Summary, and Content__[ContentType], e.g. Content-BlogPost.
            alternates.Add("Content_" + displayType);
            alternates.Add("Content__" + definition.Name);
        }

        // [Shape]_[DisplayType]__[ContentType], e.g. Content-BlogPost.Summary.
        if (!isDetail || hasStereotype)
        {
            alternates.Add(shapeType + "__" + definition.Name);
        }

        if (!hasStereotype)
        {
            alternates.Add("Content_" + displayType + "__" + definition.Name);
        }

        return new
        {
            kind = "contentItem",
            shape = shapeType,
            alternates = Describe(alternates.Distinct(StringComparer.Ordinal), displayType),
        };
    }

    private static List<object> Describe(IEnumerable<string> alternates, string displayType)
        => alternates
            .Distinct(StringComparer.Ordinal)
            .Select(name => (object)new
            {
                name,
                file = ToFileName(name, displayType),
            })
            .ToList();

    // The theme file name Orchard Core's template harvester reads as this shape name: "__" is written "-", "_" is
    // written ".", and the display type moves to the end (Content_Summary__BlogPost is Content-BlogPost.Summary).
    private static string ToFileName(string shapeName, string displayType)
    {
        var separator = shapeName.IndexOf("__", StringComparison.Ordinal);
        var head = separator < 0 ? shapeName : shapeName[..separator];
        var rest = separator < 0 ? string.Empty : shapeName[separator..];
        var suffix = "_" + displayType;
        var hasDisplayType = head.Length > suffix.Length && head.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

        if (hasDisplayType)
        {
            head = head[..^suffix.Length];
        }

        var file = (head + rest).Replace("__", "-", StringComparison.Ordinal).Replace('_', '.');

        return hasDisplayType ? file + "." + displayType : file;
    }
}
