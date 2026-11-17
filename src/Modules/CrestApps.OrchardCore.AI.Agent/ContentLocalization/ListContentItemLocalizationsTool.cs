using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentLocalization;
using OrchardCore.ContentLocalization.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.Agent.ContentLocalization;

/// <summary>
/// Lists the translations of a content item: one entry for each culture of the site.
/// </summary>
public sealed class ListContentItemLocalizationsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listContentItemLocalizations";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The ID of the content item (ContentItemId), in any of its cultures."
        }
      },
      "required": [
        "contentItemId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the translations of a content item: for each site culture, the content item ID, title and published state of its version in that culture, or that it is missing. Use it to find the item for a language or to see what still needs translating.";

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

        if (AgentToolAuthorization.GetCaller(services) is null)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("contentItemId", out var contentItemId))
        {
            return "The contentItemId argument is required.";
        }

        var contentItem = await services.GetRequiredService<IContentManager>().GetAsync(contentItemId, VersionOptions.Latest);

        if (contentItem is null)
        {
            return $"No content item with the ID '{contentItemId}' was found.";
        }

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.ViewContent, contentItem))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!contentItem.TryGet<LocalizationPart>(out var localizationPart))
        {
            return $"The content type '{contentItem.ContentType}' has no localization part, so content item '{contentItemId}' has no translations.";
        }

        var localizationService = services.GetRequiredService<ILocalizationService>();
        var supportedCultures = await localizationService.GetSupportedCulturesAsync();

        // The set holds the published and the latest version of each translation.
        var versions = string.IsNullOrEmpty(localizationPart.LocalizationSet)
            ? [contentItem]
            : (await services.GetRequiredService<IContentLocalizationManager>().GetItemsForSetAsync(localizationPart.LocalizationSet)).ToList();

        var localizations = versions
            .GroupBy(version => version.ContentItemId)
            .Select(group => new
            {
                Item = group.FirstOrDefault(version => version.Latest) ?? group.First(),
                Published = group.Any(version => version.Published),
            })
            .ToList();

        var items = new List<object>();

        foreach (var culture in supportedCultures)
        {
            var match = localizations.FirstOrDefault(entry => string.Equals(GetCulture(entry.Item), culture, StringComparison.OrdinalIgnoreCase));
            var localization = match?.Item;

            if (localization is null)
            {
                items.Add(new
                {
                    culture,
                    exists = false,
                });

                continue;
            }

            // A translation the caller may not view is reported as existing, without its details.
            var canView = localization.ContentItemId == contentItem.ContentItemId
                || await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.ViewContent, localization);

            items.Add(new
            {
                culture,
                exists = true,
                contentItemId = canView ? localization.ContentItemId : null,
                displayText = canView ? localization.DisplayText : null,
                published = canView ? match.Published : (bool?)null,
                hasDraft = canView ? !localization.Published : (bool?)null,
                isRequestedItem = localization.ContentItemId == contentItem.ContentItemId ? true : (bool?)null,
            });
        }

        return AgentToolResults.Json(new
        {
            contentItemId = contentItem.ContentItemId,
            contentType = contentItem.ContentType,
            culture = localizationPart.Culture,
            localizationSet = localizationPart.LocalizationSet,
            defaultCulture = await localizationService.GetDefaultCultureAsync(),
            items,
            total = items.Count,
        });
    }

    private static string GetCulture(ContentItem contentItem)
        => contentItem.TryGet<LocalizationPart>(out var part) ? part.Culture : null;
}
