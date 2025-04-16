using System.Text.Json;
using Microsoft.Extensions.AI;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.AI.Tools.Contents;

public sealed class PublishContentOrchardCoreTool : AIFunction
{
    public const string TheName = "publishContentItem";

    private readonly IContentManager _contentManager;

    public PublishContentOrchardCoreTool(IContentManager contentManager)
    {
        _contentManager = contentManager;

        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
            """
            {
                "type": "object",
                "properties": {
                    "contentItemId": {
                        "type": "string",
                        "description": "The string representation of the content item's ContentItemId."
                    }
                },
                "additionalProperties": false,
                "required": ["contentItemId"]
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Publishes the draft version of a content item, making it the active published version.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.TryGetValue("contentItemId", out var data))
        {
            return "Unable to find a contentItemId argument in the function arguments.";
        }

        string contentItemId;

        if (data is JsonElement jsonElement)
        {
            contentItemId = jsonElement.GetString();
        }
        else
        {
            contentItemId = data.ToString();
        }

        var contentItem = await _contentManager.GetAsync(contentItemId, VersionOptions.Draft);

        if (contentItem is null)
        {
            return $"Unable to find a content item that match the ContentItemId: {contentItemId}";
        }

        await _contentManager.PublishAsync(contentItem);

        return "Content item was successfully published";
    }
}
