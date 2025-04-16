using System.Text.Json;
using Microsoft.Extensions.AI;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.AI.Tools.Contents;

public sealed class DeleteContentOrchardCoreTool : AIFunction
{
    public const string TheName = "deleteContentItem";

    private readonly IContentManager _contentManager;

    public DeleteContentOrchardCoreTool(IContentManager contentManager)
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

    public override string Description => "Permanently removes a content item from the site, including all of its versions.";

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

        var contentItem = await _contentManager.GetAsync(contentItemId);

        if (contentItem is null)
        {
            return $"Unable to find a content item that match the ContentItemId: {contentItemId}";
        }

        await _contentManager.RemoveAsync(contentItem);

        return "Content item was successfully deleted";
    }
}
