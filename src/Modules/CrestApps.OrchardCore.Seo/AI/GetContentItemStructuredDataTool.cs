using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Seo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Seo.AI;

/// <summary>
/// Returns the JSON-LD a content item's page publishes, built by the structured data engine the page uses, for a caller
/// who may view the published item (the check runs the content access handlers, so a restricted item is refused as it
/// is on the site). Nodes that other shapes add while the page renders, such as a list's items, are not included.
/// </summary>
internal sealed class GetContentItemStructuredDataTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getContentItemStructuredData";

    /// <summary>
    /// The answer to a caller who is not signed in or may not view the item.
    /// </summary>
    public const string NotAllowedMessage = "You are not allowed to use this function.";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The id of the content item."
        }
      },
      "required": ["contentItemId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns the JSON-LD a content item's page publishes, built by the same engine as the page, or says that its content type publishes none. Use it to check the result after configuring schema.org or to answer \"what do search engines see for this item\".";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;
        var caller = GetCaller(services);

        if (caller is null)
        {
            return NotAllowedMessage;
        }

        if (arguments.Keys.FirstOrDefault(name => name != "contentItemId") is { } unknown)
        {
            return $"'{unknown}' is not an argument of {TheName}. Give only contentItemId.";
        }

        var contentItemId = arguments.TryGetValue("contentItemId", out var value) ? ReadText(value) : null;

        if (string.IsNullOrEmpty(contentItemId))
        {
            return "Give the id of the content item in contentItemId.";
        }

        var contentManager = services.GetRequiredService<IContentManager>();
        var contentItem = await contentManager.GetAsync(contentItemId, VersionOptions.Published);

        if (contentItem is null)
        {
            return await contentManager.GetAsync(contentItemId, VersionOptions.Latest) is { } draft &&
                await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(caller, CommonPermissions.ViewContent, draft)
                ? $"The content item '{contentItemId}' is not published, so it has no page and publishes no structured data."
                : $"No published content item has the id '{contentItemId}'.";
        }

        if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(caller, CommonPermissions.ViewContent, contentItem))
        {
            return NotAllowedMessage;
        }

        var settings = await services.GetRequiredService<ISiteService>().GetSettingsAsync<StructuredDataSettings>();
        var manager = services.GetRequiredService<IStructuredDataManager>();
        var entity = await manager.BuildEntityAsync(contentItem, StructuredDataPurpose.Page);
        var document = settings.RenderOnPages
            ? await manager.BuildPageDocumentAsync(contentItem)
            : null;

        return JsonSerializer.Serialize(new
        {
            contentItemId,
            displayText = contentItem.DisplayText,
            contentType = contentItem.ContentType,
            renderOnPages = settings.RenderOnPages,
            describesItem = entity is not null,
            jsonLd = document,
            message = !settings.RenderOnPages
                ? "Pages publish no structured data: rendering JSON-LD on pages is turned off in the Advanced SEO settings."
                : entity is null
                    ? $"The content type '{contentItem.ContentType}' publishes no description of its items (it has no Schema.org part, or the part is turned off for this item); the page still publishes the site's publisher and web site nodes in jsonLd."
                    : "jsonLd is the script the item's page publishes, without nodes other shapes on the page may add while it renders.",
        }, _jsonOptions);
    }

    private static ClaimsPrincipal GetCaller(IServiceProvider services)
    {
        var user = services.GetService<IUserAccessor>()?.User
            ?? services.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true
            ? user
            : null;
    }

    private static string ReadText(object value)
        => value switch
        {
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString()?.Trim(),
            JsonElement => null,
            _ => value?.ToString()?.Trim(),
        };
}
