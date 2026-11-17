using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentTypes;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

/// <summary>
/// Sets the order of the parts of a content type, or of the fields of a content part, with the content definition
/// service the drag-and-drop order of the admin editor is saved with.
/// </summary>
public sealed class ReorderContentTypeElementsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "reorderContentTypeElements";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentType": {
          "type": "string",
          "description": "To order parts: the technical name of the content type whose parts are ordered. Give contentType or part, not both."
        },
        "part": {
          "type": "string",
          "description": "To order fields: the name of the content part whose fields are ordered. For the fields of a content type itself, give the technical name of the content type here. Give contentType or part, not both."
        },
        "names": {
          "type": "array",
          "items": {
            "type": "string"
          },
          "minItems": 1,
          "description": "Every part name of the content type, or every field name of the part, in the new order. Each name is listed once."
        }
      },
      "required": [
        "names"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sets the order of the parts on a content type, or of the fields on a part, from an ordered list of names. Use it instead of editing each position. The list must name every part of the type, or every field of the part, once.";

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

        var hasContentType = arguments.TryGetFirstString("contentType", out var contentType);
        var hasPart = arguments.TryGetFirstString("part", out var part);

        if (hasContentType == hasPart)
        {
            return "Nothing was changed: give contentType to order the parts of a content type, or part to order the fields of a part, not both.";
        }

        if (!arguments.TryGetFirst<string[]>("names", out var names) || names is not { Length: > 0 })
        {
            return "Nothing was changed: the names argument must list the names in the new order.";
        }

        var contentDefinitionManager = services.GetRequiredService<IContentDefinitionManager>();
        var contentDefinitionService = services.GetRequiredService<IContentDefinitionService>();

        if (hasContentType)
        {
            var typeDefinition = await contentDefinitionManager.LoadTypeDefinitionAsync(contentType.Trim());

            if (typeDefinition is null)
            {
                return $"Nothing was changed: there is no content type named '{contentType}'.";
            }

            if (!TryGetOrder(names, typeDefinition.Parts.Select(typePart => typePart.Name), out var partNames))
            {
                return $"Nothing was changed: names must list every part of the content type '{typeDefinition.Name}' once. Its parts are: {string.Join(", ", typeDefinition.Parts.Select(typePart => typePart.Name))}.";
            }

            await contentDefinitionService.AlterTypePartsOrderAsync(typeDefinition, partNames);

            return AgentToolResults.Json(new
            {
                reordered = true,
                contentType = typeDefinition.Name,
                parts = partNames,
            });
        }

        var partDefinition = await contentDefinitionManager.LoadPartDefinitionAsync(part.Trim());

        if (partDefinition is null)
        {
            return $"Nothing was changed: there is no content part named '{part}', or the content type '{part}' has no fields of its own.";
        }

        if (!TryGetOrder(names, partDefinition.Fields.Select(field => field.Name), out var fieldNames))
        {
            return $"Nothing was changed: names must list every field of the part '{partDefinition.Name}' once. Its fields are: {string.Join(", ", partDefinition.Fields.Select(field => field.Name))}.";
        }

        await contentDefinitionService.AlterPartFieldsOrderAsync(partDefinition, fieldNames);

        return AgentToolResults.Json(new
        {
            reordered = true,
            part = partDefinition.Name,
            fields = fieldNames,
        });
    }

    /// <summary>
    /// Maps the given names to the stored names. The service adds an empty field for a name it does not know, so the
    /// list is accepted only when it names every element once.
    /// </summary>
    private static bool TryGetOrder(string[] names, IEnumerable<string> existingNames, out string[] order)
    {
        var existing = existingNames.ToList();
        var result = new List<string>();

        foreach (var name in names)
        {
            var match = existing.FirstOrDefault(existingName => string.Equals(existingName, name?.Trim(), StringComparison.OrdinalIgnoreCase));

            if (match is null || result.Contains(match))
            {
                order = null;

                return false;
            }

            result.Add(match);
        }

        order = result.ToArray();

        return result.Count == existing.Count;
    }
}
