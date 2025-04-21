using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
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
                  "description": "The name of the Orchard Core content type to generate a sample JSON structure for."
                }
              },
              "required": ["contentType"],
              "additionalProperties": false
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Creates a new content item or updates an existing one by creating a new version.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.TryGetFirstString("contentType", out var contentType))
        {
            return "Unable to find a contentType argument in the function arguments.";
        }

        if (await _contentDefinitionManager.GetTypeDefinitionAsync(contentType) is null)
        {
            return "The given content type does not exists";
        }

        var contentItem = await _contentManager.NewAsync(contentType);

        return JsonSerializer.Serialize(contentItem, _options.SerializerOptions);
    }
}
