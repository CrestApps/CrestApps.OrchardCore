using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Autoroute;
using OrchardCore.Autoroute.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Routing;
using OrchardCore.Contents;
using OrchardCore.Settings;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Agent.Autoroute;

/// <summary>
/// Makes a published content item the home page of the site, the way the content editor does: it ticks "Set as
/// homepage" on the Autoroute part and publishes the content item, and the Autoroute feature writes the home route.
/// </summary>
public sealed class SetHomepageTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setHomepage";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The ID of the published content item (ContentItemId) that becomes the home page."
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
    public override string Description => "Makes a published content item the site's home page. The item's type must have an Autoroute part with a path and the route must not be disabled. Every visitor sees the change at once; the result names the previous home page, which the site does not remember.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AutoroutePermissions.SetHomepage))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("contentItemId", out var contentItemId))
        {
            return "The contentItemId argument is required.";
        }

        var contentManager = services.GetRequiredService<IContentManager>();
        var latest = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);

        if (latest is null)
        {
            return $"No content item with the ID '{contentItemId}' was found.";
        }

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.PublishContent, latest))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        // Setting the home page publishes the content item, so an unpublished draft would go live with it.
        if (!latest.Published)
        {
            return $"Nothing was changed: the latest version of content item '{latest.ContentItemId}' is not published. Publish it, or discard its draft, and then set it as the home page.";
        }

        if (!latest.TryGet<AutoroutePart>(out var autoroutePart))
        {
            return $"Nothing was changed: content item '{latest.ContentItemId}' has no Autoroute part, so it has no address to show at the home page. Add the Autoroute part to the content type '{latest.ContentType}' first.";
        }

        if (string.IsNullOrWhiteSpace(autoroutePart.Path))
        {
            return $"Nothing was changed: content item '{latest.ContentItemId}' has no path in its Autoroute part. Give it a path first.";
        }

        if (autoroutePart.Disabled)
        {
            return $"Nothing was changed: the route of content item '{latest.ContentItemId}' is disabled in its Autoroute part. Enable it first.";
        }

        var siteService = services.GetRequiredService<ISiteService>();
        var options = services.GetRequiredService<IOptions<AutorouteOptions>>().Value;
        var previousSite = await siteService.GetSiteSettingsAsync();
        var previousContentItemId = HomeRouteContent.GetContentItemId(previousSite, options);
        var previousHomeRoute = previousContentItemId is null
            ? HomeRouteContent.Describe(previousSite)
            : null;

        if (string.Equals(previousContentItemId, latest.ContentItemId, StringComparison.OrdinalIgnoreCase) &&
            HomeRouteContent.GetJsonPath(previousSite, options) is null)
        {
            return AgentToolResults.Json(new
            {
                changed = false,
                contentItemId = latest.ContentItemId,
                displayText = latest.DisplayText,
                note = "This content item is already the home page.",
            });
        }

        // The steps of the content editor: a draft of the content item with the flag set, then publish. The Autoroute
        // feature writes the home route when the content item is published and clears the flag.
        var contentItem = await contentManager.GetAsync(latest.ContentItemId, VersionOptions.DraftRequired);

        contentItem.Alter<AutoroutePart>(part => part.SetHomepage = true);

        await contentManager.UpdateAsync(contentItem);

        var result = await contentManager.ValidateAsync(contentItem);

        if (!result.Succeeded)
        {
            await services.GetRequiredService<YSession>().CancelAsync();

            return $"Nothing was changed: the content item is not valid. {string.Join(" ", result.Errors.Select(error => error.ErrorMessage))}".TrimEnd();
        }

        await contentManager.PublishAsync(contentItem);

        var site = await siteService.LoadSiteSettingsAsync();

        if (!contentItem.Published ||
            !string.Equals(HomeRouteContent.GetContentItemId(site, options), contentItem.ContentItemId, StringComparison.OrdinalIgnoreCase))
        {
            return $"The home page was not changed: content item '{contentItem.ContentItemId}' was saved, but the site's home route does not point to it. Check that the content item is published and that the Autoroute feature is enabled.";
        }

        return AgentToolResults.Json(new
        {
            changed = true,
            contentItemId = contentItem.ContentItemId,
            contentItemVersionId = contentItem.ContentItemVersionId,
            displayText = contentItem.DisplayText,
            path = autoroutePart.Path,
            previousContentItemId,
            previousHomeRoute,
            note = previousContentItemId is null
                ? "The content item is the home page now. As in the content editor, it was published again to set it."
                : "The content item is the home page now. As in the content editor, it was published again to set it. The site does not remember the previous home page: to go back, set previousContentItemId as the home page.",
        });
    }
}
