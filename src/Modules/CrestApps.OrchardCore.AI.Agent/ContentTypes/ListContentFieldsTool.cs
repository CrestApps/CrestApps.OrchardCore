using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Services;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

public sealed class ListContentFieldsTool : AIFunction
{
    public const string TheName = "listContentFieldDefinitions";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Retrieves the available content fields which can be used to create content parts.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var contentMetadataService = arguments.Services.GetRequiredService<ContentMetadataService>();

        if (!await arguments.IsAuthorizedAsync(OrchardCorePermissions.ViewContentTypes))
        {
            return "You do not have permission to view content types.";
        }

        var fieldTypes = await contentMetadataService.GetFieldsAsync();

        return JsonSerializer.Serialize(fieldTypes.Select(fieldType => fieldType.Name));
    }
}
