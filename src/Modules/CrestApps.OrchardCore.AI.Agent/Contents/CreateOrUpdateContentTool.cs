using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Settings;
using CrestApps.Core.AI.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using Usr = OrchardCore.Users;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

/// <summary>
/// AI tool that performs create or update content operations.
/// </summary>
public sealed class CreateOrUpdateContentTool : AIFunction
{
    private const string ContentItemArgumentDescription = "The top-level Orchard Core content item to create or update. Can be a JSON object or a JSON-encoded string, but it must match the Orchard Core ContentItem JSON shape for the target content type. Before calling this function, call the 'getContentItemSchema' tool first whenever it is available and request the parent content type plus any nested content types you plan to include so the payload matches the current Orchard Core schema. If the item contains nested or contained content items, include them inside this parent payload instead of calling the function separately for each nested item. To perform an update, include a valid 'ContentItemId'.";

    /// <summary>
    /// The name constant.
    /// </summary>
    public const string TheName = "createOrUpdateContentItem";

    private static readonly JsonMergeSettings _updateJsonMergeSettings = new()
    {
        MergeArrayHandling = MergeArrayHandling.Replace,
    };

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    $$"""
    {
      "type": "object",
      "properties": {
        "contentItem": {
          "description": "{{ContentItemArgumentDescription}}"
        },
        "isDraft": {
          "type": "boolean",
          "description": "Indicates whether the content item should be saved as a draft. If set to false, the item will be published immediately."
        },
        "ownerUsername": {
          "type": "string",
          "description": "Optional. The username of the user who should own the content item. Used as a fallback when no user is authenticated."
        },
        "ownerUserId": {
          "type": "string",
          "description": "Optional. The user ID of the user who should own the content item. Used as a fallback when no user is authenticated."
        },
        "ownerEmail": {
          "type": "string",
          "description": "Optional. The email of the user who should own the content item. Used as a fallback when no user is authenticated."
        }
      },
      "required": [
        "contentItem",
        "isDraft"
      ],
      "additionalProperties": false
    }
    """);

    public override string Name => TheName;

    public override string Description
        =>
        $"""
        Creates a new content item or updates an existing one by creating a new version. {ContentItemToolGuidance.ContentSchemaInstruction}
        {ContentItemToolGuidance.NestedContentInstruction}
        Rejects payloads when the JSON does not match the expected Orchard Core content item structure.
        """;

    public override JsonElement JsonSchema => _jsonSchema;

    /// <summary>
    /// Gets the additional properties for the AI function, such as strict mode configuration.
    /// </summary>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<CreateOrUpdateContentTool>>();
        var payloadAssistanceService = arguments.Services.GetRequiredService<IContentItemPayloadAssistanceService>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", TheName);
        }

        // Accept contentItem as either a JSON string or a JSON object.
        // Models often send an object even when the schema specifies string.
        string json;

        if (arguments.TryGetFirstString("contentItem", out var str))
        {
            json = str;
        }
        else if (arguments.TryGetFirst("contentItem", out var raw) && raw is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            json = je.GetRawText();
        }

        else
        {
            logger.LogWarning("AI tool '{ToolName}': Unable to find a contentItem argument in the function arguments.", TheName);

            return "Unable to find a contentItem argument in the function arguments.";
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

        var inputNode = JsonNode.Parse(doc.RootElement.GetRawText()) as JsonObject;

        if (inputNode is null)
        {
            logger.LogWarning("AI tool '{ToolName}': The contentItem argument must be a JSON object.", TheName);

            return "The contentItem argument must be a JSON object.";
        }

        var model = doc.RootElement.Deserialize<ContentItem>(JsonSerializerOptions);

        if (model is null)
        {
            logger.LogWarning("AI tool '{ToolName}': The contentItem argument could not be mapped to a content item.", TheName);

            return "The contentItem argument could not be mapped to a content item.";
        }

        var contentManager = arguments.Services.GetRequiredService<IContentManager>();
        var contentDefinitionManager = arguments.Services.GetRequiredService<IContentDefinitionManager>();

        var contentItem = await contentManager.GetAsync(model.ContentItemId, VersionOptions.DraftRequired);
        var resolvedContentType = contentItem?.ContentType ?? model.ContentType;

        if (contentItem is null)
        {
            if (string.IsNullOrEmpty(resolvedContentType))
            {
                logger.LogWarning("AI tool '{ToolName}': A Content type is required to create a new content item.", TheName);

                return "A Content type is required";
            }
        }

        var contentType = resolvedContentType!;
        var contentDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(contentType);

        if (contentDefinition is null)
        {
            logger.LogWarning("AI tool '{ToolName}': Invalid content type '{ContentType}'.", TheName, contentType);

            return $"Invalid content type '{contentType}'. In this is a new content type, first create content type definition then created the content item.";
        }

        var payloadValidationResult = await payloadAssistanceService.ValidateAsync(
            contentDefinition,
            inputNode,
            cancellationToken);

        if (!payloadValidationResult.IsValid)
        {
            logger.LogWarning(
                "AI tool '{ToolName}': The content item JSON failed validation for '{ContentType}'. Messages: {Messages}. Unmapped paths: {Paths}.",
                TheName,
                contentDefinition.Name,
                string.Join(" | ", payloadValidationResult.Messages),
                payloadValidationResult.UnmappedPaths.Count == 0
                    ? "(none)"
                    : string.Join(", ", payloadValidationResult.UnmappedPaths));

            return await CreateIncorrectStructureResponseAsync(
                payloadAssistanceService,
                contentDefinition,
                payloadValidationResult,
                cancellationToken);
        }

        if (contentItem is null)
        {
            contentItem = await contentManager.NewAsync(contentType);

            contentItem.Merge(model);

            // When no user is authenticated, try to resolve an owner from optional parameters
            // so that contentItem.Owner is set correctly.
            await TrySetOwnerAsync(arguments, contentItem);

            var result = await contentManager.ValidateAsync(contentItem);

            if (!result.Succeeded)
            {
                logger.LogWarning("AI tool '{ToolName}': Unable to create content item due to validation errors: {Errors}.", TheName, string.Join(", ", result.Errors.Select(x => x.ErrorMessage)));

                return await CreateValidationFailureResponseAsync(
                    payloadAssistanceService,
                    contentDefinition,
                    "Unable to create the content item",
                    result.Errors.Select(x => x.ErrorMessage),
                    cancellationToken);
            }

            await contentManager.CreateAsync(contentItem, VersionOptions.Draft);
        }

        else
        {
            contentItem.Merge(model, _updateJsonMergeSettings);

            await contentManager.UpdateAsync(contentItem);

            var result = await contentManager.ValidateAsync(contentItem);

            if (!result.Succeeded)
            {
                logger.LogWarning("AI tool '{ToolName}': Unable to update content item due to validation errors: {Errors}.", TheName, string.Join("; ", result.Errors.Select(x => x.ErrorMessage)));

                return await CreateValidationFailureResponseAsync(
                    payloadAssistanceService,
                    contentDefinition,
                    "Unable to update the content item",
                    result.Errors.Select(x => x.ErrorMessage),
                    cancellationToken);
            }
        }

        string response;

        if (isDraft)
        {
            await contentManager.SaveDraftAsync(contentItem);

            response = $"A draft content item with id '{contentItem.ContentItemId}' was successfully saved.";
        }

        else
        {
            await contentManager.PublishAsync(contentItem);

            response = $"A content item with id '{contentItem.ContentItemId}' was successfully published.";
        }

        // Flush the changes to allow other tools to access it in the same function execution, such as a tool that generates a link to the content item after creation.
        var session = arguments.Services.GetRequiredService<YSession>();
        await session.FlushAsync(cancellationToken);

        // HttpContext may be null when invoked from a background task (e.g., post-session processing).
        var httpContextAccessor = arguments.Services.GetRequiredService<IHttpContextAccessor>();
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext is not null)
        {
            var linkGenerator = arguments.Services.GetRequiredService<LinkGenerator>();
            var metadata = await contentManager.PopulateAspectAsync<ContentItemMetadata>(contentItem);

            if (metadata.AdminRouteValues is not null)
            {
                response = $"""
                {response}
                The edit URI is: {linkGenerator.GetUriByRouteValues(httpContext, null, metadata.AdminRouteValues)}
                """;
            }

            if (metadata.DisplayRouteValues is not null)
            {
                response = $"""
                {response}
                The view URI is: {linkGenerator.GetUriByRouteValues(httpContext, null, metadata.DisplayRouteValues)}
                """;
            }
        }
        else if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}': HttpContext is null (likely running in a background task). Skipping URI generation.", TheName);
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", TheName);
        }

        return response;
    }

    /// <summary>
    /// Attempts to resolve a content owner from optional tool parameters when no user is authenticated.
    /// This allows the AI model to specify who should own the content item when invoked from anonymous contexts.
    /// </summary>
    private static async Task TrySetOwnerAsync(AIFunctionArguments arguments, ContentItem contentItem)
    {
        var httpContextAccessor = arguments.Services.GetRequiredService<IHttpContextAccessor>();
        var principal = httpContextAccessor.HttpContext?.User;

        // If a user is already authenticated, the content handlers will set the owner automatically.
        if (principal?.Identity?.IsAuthenticated == true)
        {
            return;
        }

        var userManager = arguments.Services.GetRequiredService<UserManager<Usr.IUser>>();

        Usr.IUser user = null;

        if (arguments.TryGetFirstString("ownerUserId", out var ownerUserId))
        {
            user = await userManager.FindByIdAsync(ownerUserId);
        }

        if (user is null && arguments.TryGetFirstString("ownerUsername", out var ownerUsername))
        {
            user = await userManager.FindByNameAsync(ownerUsername);
        }

        if (user is null && arguments.TryGetFirstString("ownerEmail", out var ownerEmail))
        {
            user = await userManager.FindByEmailAsync(ownerEmail);
        }

        if (user is not null)
        {
            contentItem.Owner = await userManager.GetUserIdAsync(user);
            contentItem.Author = user.UserName;
        }
    }

    private static async Task<string> CreateIncorrectStructureResponseAsync(
        IContentItemPayloadAssistanceService payloadAssistanceService,
        ContentTypeDefinition contentDefinition,
        ContentItemPayloadValidationResult validationResult,
        CancellationToken cancellationToken)
    {
        var details = new List<string>(validationResult.Messages);
        var guidance = validationResult.Guidance;

        if (validationResult.UnmappedPaths.Count > 0)
        {
            details.Add($"Unmapped or misplaced paths: {string.Join(", ", validationResult.UnmappedPaths)}.");
        }

        if (string.IsNullOrWhiteSpace(guidance))
        {
            guidance = await payloadAssistanceService.GetGuidanceAsync(contentDefinition, cancellationToken);
        }

        return
        $"""
        The provided content item JSON could not be mapped completely.
        This usually means the JSON shape does not match the Orchard Core content item structure for '{contentDefinition.Name}'.
        {string.Join(Environment.NewLine, details)}
        Please call the function again using the valid structure shown below. When a JSON schema is provided, treat it as the contract for the retry payload.

        {ContentItemToolGuidance.EnsureNestedContentInstruction(guidance)}
        """;
    }

    private static async Task<string> CreateValidationFailureResponseAsync(
        IContentItemPayloadAssistanceService payloadAssistanceService,
        ContentTypeDefinition contentDefinition,
        string operation,
        IEnumerable<string> errors,
        CancellationToken cancellationToken)
    {
        var guidance = await payloadAssistanceService.GetGuidanceAsync(contentDefinition, cancellationToken);

        return
        $"""
        {operation} due to the following errors: {string.Join(", ", errors)}.

        {ContentItemToolGuidance.EnsureNestedContentInstruction(guidance)}
        """;
    }
}
