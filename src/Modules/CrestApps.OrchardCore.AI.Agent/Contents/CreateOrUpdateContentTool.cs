using System.Text;
using System.Text.Json;
using System.Text.Json.Settings;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;
using YesSql;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

public sealed class CreateOrUpdateContentTool : AIFunction
{
    public const string TheName = "createOrUpdateContentItem";

    private static readonly JsonMergeSettings _updateJsonMergeSettings = new()
    {
        MergeArrayHandling = MergeArrayHandling.Replace,
    };

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "contentItem": {
              "type": "string",
              "description": "A JSON string representing the content item to create or update. To perform an update, the object must include a valid 'ContentItemId'."
            },
            "isDraft": {
              "type": "boolean",
              "description": "Indicates whether the content item should be saved as a draft. If set to false, the item will be published immediately."
            }
          },
          "required": ["contentItem", "isDraft"],
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Creates a new content item or updates an existing one by creating a new version.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        if (!arguments.TryGetFirstString("contentItem", out var json))
        {
            return "Unable to find a contentItemId argument in the function arguments.";
        }

        if (!arguments.TryGetFirst<bool>("isDraft", out var isDraft))
        {
            isDraft = false;
        }

        // Use Utf8JsonReader + JsonDocument.ParseValue to read only the first complete
        // JSON value, ignoring any trailing characters the model may have appended.
        var bytes = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);
        using var doc = JsonDocument.ParseValue(ref reader);
        var model = doc.RootElement.Deserialize<ContentItem>(JsonSerializerOptions);

        var contentManager = arguments.Services.GetRequiredService<IContentManager>();

        var contentItem = await contentManager.GetAsync(model.ContentItemId, VersionOptions.DraftRequired);
        var session = arguments.Services.GetRequiredService<ISession>();

        if (contentItem is null)
        {
            if (string.IsNullOrEmpty(model?.ContentType))
            {
                return "A Content type is required";
            }

            var contentDefinitionManager = arguments.Services.GetRequiredService<IContentDefinitionManager>();
            var contentDefintions = await contentDefinitionManager.GetTypeDefinitionAsync(model.ContentType);

            if (contentDefintions is null)
            {
                return $"Invalid content type '{model.ContentType}'. In this is a new content type, first create content type definition then created the content item.";
            }

            contentItem = await contentManager.NewAsync(model.ContentType);

            if (!await arguments.IsAuthorizedAsync(CommonPermissions.PublishContent, contentItem))
            {
                return "The current user does not have permission to publish the content item";
            }

            contentItem.Merge(model);

            var result = await contentManager.ValidateAsync(contentItem);

            if (!result.Succeeded)
            {
                return
                   $"""
                    Unable to create the content item due to the following errors: {string.Join(", ", result.Errors.Select(x => x.ErrorMessage))}.
                    For reference, here is the correct content type definition {JsonSerializer.Serialize(contentDefintions, JsonHelpers.ContentDefinitionSerializerOptions)}
                    """;
            }
            else
            {
                await contentManager.CreateAsync(contentItem, VersionOptions.Draft);
            }
        }
        else
        {
            if (!await arguments.IsAuthorizedAsync(CommonPermissions.EditContent, contentItem))
            {
                return "The current user does not have permission to edit the content item";
            }

            contentItem.Merge(model, _updateJsonMergeSettings);

            await contentManager.UpdateAsync(contentItem);

            var result = await contentManager.ValidateAsync(contentItem);

            if (!result.Succeeded)
            {
                return "Unable to update the content item due to the following errors: " + string.Join(';', result.Errors.Select(x => x.ErrorMessage));
            }
        }

        if (isDraft)
        {
            await contentManager.SaveDraftAsync(contentItem);

            await session.SaveChangesAsync(cancellationToken);

            return $"A draft content item with id '{contentItem.ContentItemId}' was successfully saved.";
        }
        else
        {
            await contentManager.PublishAsync(contentItem);

            await session.SaveChangesAsync(cancellationToken);

            return $"A content item with id '{contentItem.ContentItemId}' was successfully published.";
        }
    }
}
