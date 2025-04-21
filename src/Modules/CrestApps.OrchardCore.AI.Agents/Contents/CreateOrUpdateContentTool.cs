using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Settings;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.AI.Agents.Contents;

public sealed class CreateOrUpdateContentTool : AIFunction
{
    public const string TheName = "createOrUpdateContentItem";

    private static readonly JsonMergeSettings _updateJsonMergeSettings = new()
    {
        MergeArrayHandling = MergeArrayHandling.Replace,
    };

    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public CreateOrUpdateContentTool(
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;

        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
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
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Creates a new content item or updates an existing one by creating a new version.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.TryGetFirstString("contentItem", out var json))
        {
            return "Unable to find a contentItemId argument in the function arguments.";
        }

        if (!arguments.TryGetFirst<bool>("isDraft", out var isDraft))
        {
            isDraft = false;
        }

        var model = JsonSerializer.Deserialize<ContentItem>(json, JsonSerializerOptions);

        var contentItem = await _contentManager.GetAsync(model.ContentItemId, VersionOptions.DraftRequired);

        if (contentItem is null)
        {
            if (string.IsNullOrEmpty(model?.ContentType))
            {
                return "A Content type is required";
            }

            if (await _contentDefinitionManager.GetTypeDefinitionAsync(model.ContentType) == null)
            {
                return "Unknown content type. Before creating content item, first create content type definition.";
            }

            contentItem = await _contentManager.NewAsync(model.ContentType);
            contentItem.Owner = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, CommonPermissions.PublishContent, contentItem))
            {
                return "The current user does not have permission to publish the content item";
            }

            contentItem.Merge(model);

            var result = await _contentManager.ValidateAsync(contentItem);

            if (!result.Succeeded)
            {
                return "Unable to create the content item due to the following errors: " + string.Join(", ", result.Errors.Select(x => x.ErrorMessage));
            }
            else
            {
                await _contentManager.CreateAsync(contentItem, VersionOptions.Draft);
            }
        }
        else
        {
            if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, CommonPermissions.EditContent, contentItem))
            {
                return "The current user does not have permission to edit the content item";
            }

            contentItem.Merge(model, _updateJsonMergeSettings);

            await _contentManager.UpdateAsync(contentItem);

            var result = await _contentManager.ValidateAsync(contentItem);

            if (!result.Succeeded)
            {
                return "Unable to update the content item due to the following errors: " + string.Join(", ", result.Errors.Select(x => x.ErrorMessage));
            }
        }

        if (isDraft)
        {
            await _contentManager.SaveDraftAsync(contentItem);

            return "A draft content item was successfully saved.";
        }
        else
        {
            await _contentManager.PublishAsync(contentItem);

            return "A content item was successfully published.";
        }
    }
}
