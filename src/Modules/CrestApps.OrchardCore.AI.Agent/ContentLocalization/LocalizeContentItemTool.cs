using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentLocalization;
using OrchardCore.ContentLocalization.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.Agent.ContentLocalization;

/// <summary>
/// Creates the version of a content item in another culture, as the Localize action of the content editor does.
/// </summary>
public sealed class LocalizeContentItemTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "localizeContentItem";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The ID of the content item (ContentItemId) to translate."
        },
        "targetCulture": {
          "type": "string",
          "description": "The culture of the new version, for example fr or es-MX. It must be one of the site's supported cultures."
        }
      },
      "required": [
        "contentItemId",
        "targetCulture"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates the version of a content item in another culture as a draft copy linked to the same localization set, and returns the new content item ID. Fails if that culture already exists or the type has no localization part; translate the copy with createOrUpdateContentItem afterwards.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;
        var user = AgentToolAuthorization.GetCaller(services);

        if (user is null)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("contentItemId", out var contentItemId))
        {
            return "The contentItemId argument is required.";
        }

        if (!arguments.TryGetFirstString("targetCulture", out var targetCulture))
        {
            return "The targetCulture argument is required.";
        }

        var contentItem = await services.GetRequiredService<IContentManager>().GetAsync(contentItemId, VersionOptions.Latest);

        if (contentItem is null)
        {
            return $"No content item with the ID '{contentItemId}' was found.";
        }

        // The two checks of the Localize action: localize this item, and edit content of its type.
        var authorizationService = services.GetRequiredService<IAuthorizationService>();

        if (!await authorizationService.AuthorizeAsync(user, ContentLocalizationPermissions.LocalizeContent, contentItem) ||
            !await authorizationService.AuthorizeContentTypeAsync(user, CommonPermissions.EditContent, contentItem.ContentType, user.FindFirstValue(ClaimTypes.NameIdentifier)))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!contentItem.TryGet<LocalizationPart>(out var localizationPart))
        {
            return $"Nothing was created: the content type '{contentItem.ContentType}' has no localization part, so its items cannot be translated.";
        }

        var supportedCultures = await services.GetRequiredService<ILocalizationService>().GetSupportedCulturesAsync();
        var culture = supportedCultures.FirstOrDefault(supported => string.Equals(supported, targetCulture.Trim(), StringComparison.OrdinalIgnoreCase));

        if (culture is null)
        {
            return $"Nothing was created: '{targetCulture}' is not a supported culture of the site. The supported cultures are: {string.Join(", ", supportedCultures)}.";
        }

        var contentLocalizationManager = services.GetRequiredService<IContentLocalizationManager>();

        if (!string.IsNullOrEmpty(localizationPart.LocalizationSet))
        {
            var existing = await contentLocalizationManager.GetContentItemAsync(localizationPart.LocalizationSet, culture);

            if (existing is not null)
            {
                return $"Nothing was created: a '{culture}' version already exists. Its content item ID is '{existing.ContentItemId}'.";
            }
        }

        ContentItem localized;

        try
        {
            localized = await contentLocalizationManager.LocalizeAsync(contentItem, culture);
        }
        catch (InvalidOperationException)
        {
            return $"The '{culture}' version of content item '{contentItemId}' could not be created.";
        }

        return AgentToolResults.Json(new
        {
            localized = true,
            sourceContentItemId = contentItem.ContentItemId,
            contentItemId = localized.ContentItemId,
            contentType = localized.ContentType,
            culture,
            displayText = localized.DisplayText,
            published = localized.Published,
            note = "The new version is a draft copy that still holds the text of the source. Translate it with createOrUpdateContentItem, then publish it.",
        });
    }
}
