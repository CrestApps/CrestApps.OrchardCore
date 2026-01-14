using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

public sealed class GetContentItemSchemaTool : AIFunction
{
    public const string TheName = "getSampleContentItemForContentType";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "contentType": {
              "type": "string",
              "description": "The name of the Orchard Core content type to generate a sample JSON structure for."
            }
          },
          "required": ["contentType"],
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Creates a new content item or updates an existing one by creating a new version.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var contentManager = arguments.Services.GetRequiredService<IContentManager>();
        var contentDefinitionManager = arguments.Services.GetRequiredService<IContentDefinitionManager>();
        var options = arguments.Services.GetRequiredService<IOptions<DocumentJsonSerializerOptions>>().Value;

        if (!arguments.TryGetFirstString("contentType", out var contentType))
        {
            return "Unable to find a contentType argument in the function arguments.";
        }

        if (await contentDefinitionManager.GetTypeDefinitionAsync(contentType) is null)
        {
            return "The given content type does not exists";
        }

        var contentItem = await contentManager.NewAsync(contentType);

        return JsonSerializer.Serialize(contentItem, options.SerializerOptions);
    }
}
