using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.AI.Tools.ContentTypes;

public sealed class GetContentTypeDefinitionsOrchardCoreTool : AIFunction
{
    public const string TheName = "getContentTypeDefinition";

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly DocumentJsonSerializerOptions _options;

    public GetContentTypeDefinitionsOrchardCoreTool(
        IContentDefinitionManager contentDefinitionManager,
        IOptions<DocumentJsonSerializerOptions> options)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _options = options.Value;

        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
            """
            {
                "type": "object",
                "properties": {
                    "contentType": {
                        "type": "string",
                        "description": "The content type to get the definitions for."
                    }
                },
                "additionalProperties": false,
                "required": ["contentType"]
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Retrieves the content type definition for a given content type.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.TryGetValue("contentType", out var data))
        {
            return "Unable to find a contentType argument in the function arguments.";
        }

        string contentType;

        if (data is JsonElement jsonElement)
        {
            contentType = jsonElement.GetString();
        }
        else
        {
            contentType = data.ToString();
        }

        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

        if (definition is null)
        {
            return $"Unable to find a type definition that match the ContentType: {contentType}";
        }

        return JsonSerializer.Serialize(definition, _options.SerializerOptions);
    }
}
