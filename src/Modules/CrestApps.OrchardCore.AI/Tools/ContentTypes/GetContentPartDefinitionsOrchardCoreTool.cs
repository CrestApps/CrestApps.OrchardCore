using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.AI.Tools.ContentTypes;

public sealed class GetContentPartDefinitionsOrchardCoreTool : AIFunction
{
    public const string TheName = "getContentPartDefinition";

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly DocumentJsonSerializerOptions _options;

    public GetContentPartDefinitionsOrchardCoreTool(
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
                    "contentPart": {
                        "type": "string",
                        "description": "The content part to get the definitions for."
                    }
                },
                "additionalProperties": false,
                "required": ["contentPart"]
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Retrieves the content part definition for a given content part.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.TryGetValue("contentPart", out var data))
        {
            return "Unable to find a contentPart argument in the function arguments.";
        }

        string contentPart;

        if (data is JsonElement jsonElement)
        {
            contentPart = jsonElement.GetString();
        }
        else
        {
            contentPart = data.ToString();
        }

        var definition = await _contentDefinitionManager.GetPartDefinitionAsync(contentPart);

        if (definition is null)
        {
            return $"Unable to find a part definition that match the ContentPart: {contentPart}";
        }

        return JsonSerializer.Serialize(definition, _options.SerializerOptions);
    }
}
