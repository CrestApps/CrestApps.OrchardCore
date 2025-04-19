using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.AI.Agents.Contents;

public sealed class GetContentItemSchemaTool : AIFunction
{
    public const string TheName = "getSampleContentItemForContentType";

    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly DocumentJsonSerializerOptions _options;

    public GetContentItemSchemaTool(
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        IOptions<DocumentJsonSerializerOptions> options)
    {
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _options = options.Value;
        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
            """
            {
                "type": "object",
                "properties": {
                    "contentType": {
                        "type": "string",
                        "description": "Generates a sample Orchard Core content item JSON structure for the specified content type."
                    }
                },
                "additionalProperties": false,
                "required": ["contentType"]
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Creates a new content item or updates an existing one by creating a new version.";

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

        if (string.IsNullOrEmpty(contentType))
        {
            return "Invalid contentType argument.";
        }

        if (await _contentDefinitionManager.GetTypeDefinitionAsync(contentType) is null)
        {
            return "The given content type does not exists";
        }

        var contentItem = await _contentManager.NewAsync(contentType);

        return JsonSerializer.Serialize(contentItem, _options.SerializerOptions);
    }
}
