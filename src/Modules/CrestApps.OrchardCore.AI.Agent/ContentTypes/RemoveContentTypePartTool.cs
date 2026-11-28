using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentTypes;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

/// <summary>
/// Removes a part from a content type, with the content definition service the content type editor of the admin uses.
/// </summary>
public sealed class RemoveContentTypePartTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "removeContentTypePart";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentType": {
          "type": "string",
          "description": "The technical name of the content type, for example BlogPost."
        },
        "name": {
          "type": "string",
          "description": "The name of the part on the content type: the part name, for example AutoroutePart, or the technical name of a named part, for example Gallery."
        }
      },
      "required": [
        "contentType",
        "name"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Removes a part from a content type. Existing items keep the stored data but it is no longer edited or displayed; a part that owns the type's fields cannot be re-created with the same data automatically.";

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

        if (!arguments.TryGetFirstString("contentType", out var contentType))
        {
            return "The contentType argument is required.";
        }

        if (!arguments.TryGetFirstString("name", out var name))
        {
            return "The name argument is required.";
        }

        var typeDefinition = await services.GetRequiredService<IContentDefinitionManager>().LoadTypeDefinitionAsync(contentType);

        if (typeDefinition is null)
        {
            return $"Nothing was removed: there is no content type named '{contentType}'.";
        }

        var typePart = typeDefinition.Parts.FirstOrDefault(part => string.Equals(part.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

        if (typePart is null)
        {
            return $"Nothing was removed: the content type '{typeDefinition.Name}' has no part named '{name}'. Its parts are: {string.Join(", ", typeDefinition.Parts.Select(part => part.Name))}.";
        }

        try
        {
            await services.GetRequiredService<IContentDefinitionService>().RemovePartFromTypeAsync(typePart.Name, typeDefinition.Name);
        }
        catch (InvalidOperationException)
        {
            return $"Nothing was removed: the part '{typePart.Name}' is defined by the system and cannot be removed from the content type '{typeDefinition.Name}'.";
        }

        return AgentToolResults.Json(new
        {
            removed = true,
            contentType = typeDefinition.Name,
            name = typePart.Name,
            part = typePart.PartDefinition.Name,
        });
    }
}
