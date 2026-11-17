using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentTypes;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

/// <summary>
/// Removes a field from a content part, with the content definition service the content definition editor of the admin
/// uses.
/// </summary>
public sealed class RemoveContentPartFieldTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "removeContentPartField";

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
          "description": "The technical name of the field to remove."
        }
      },
      "required": [
        "part",
        "name"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Removes a field from a content part or content type. Existing items keep the stored values but they are no longer edited or displayed.";

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

        if (!arguments.TryGetFirstString("part", out var part))
        {
            return "The part argument is required.";
        }

        if (!arguments.TryGetFirstString("name", out var name))
        {
            return "The name argument is required.";
        }

        var partDefinition = await services.GetRequiredService<IContentDefinitionManager>().LoadPartDefinitionAsync(part.Trim());

        if (partDefinition is null)
        {
            return $"Nothing was removed: there is no content part named '{part}', or the content type '{part}' has no fields of its own.";
        }

        var field = partDefinition.Fields.FirstOrDefault(existing => string.Equals(existing.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

        if (field is null)
        {
            return $"Nothing was removed: the part '{partDefinition.Name}' has no field named '{name}'. Its fields are: {string.Join(", ", partDefinition.Fields.Select(existing => existing.Name))}.";
        }

        try
        {
            await services.GetRequiredService<IContentDefinitionService>().RemoveFieldFromPartAsync(field.Name, partDefinition.Name);
        }
        catch (InvalidOperationException)
        {
            return $"Nothing was removed: the part '{partDefinition.Name}' is defined by the system, so its fields cannot be removed.";
        }

        return AgentToolResults.Json(new
        {
            removed = true,
            part = partDefinition.Name,
            name = field.Name,
            fieldType = field.FieldDefinition.Name,
        });
    }
}
