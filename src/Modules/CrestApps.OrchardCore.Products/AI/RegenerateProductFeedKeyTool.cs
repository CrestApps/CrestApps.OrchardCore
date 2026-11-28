using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Products.Feeds;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Entities;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Products.AI;

/// <summary>
/// Makes a new access key for the product feed addresses, or removes it, as the key buttons of Settings › Commerce ›
/// Product feeds do (<see cref="ProductFeedAddresses.NewKey"/>). The key is a secret and is never returned: the settings
/// page shows the new addresses in full. It needs <c>ManageProductFeeds</c> and asks for approval, because a new key stops
/// every address already given to a shopping channel.
/// </summary>
internal sealed class RegenerateProductFeedKeyTool : ProductsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "regenerateProductFeedKey";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "action": {
          "type": "string",
          "enum": ["new", "remove"],
          "description": "new to make a new access key (the feed then needs it), remove to remove the key so the feed is public."
        }
      },
      "required": ["action"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Makes a new access key for the product feed addresses, or removes the key so the feed is public. A new key stops every feed address already given to a shopping channel from working until it is updated there. The key itself is never shown; the settings page shows the new addresses.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ProductFeedPermissions.ManageProductFeeds;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var action = ReadString(arguments, "action")?.ToLowerInvariant();

        if (action is not ("new" or "remove"))
        {
            return "Nothing was changed: give action new to make a new access key, or remove to make the feed public.";
        }

        var siteService = services.GetRequiredService<ISiteService>();
        var site = await siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<ProductFeedSettings>();

        settings.Key = action == "new" ? ProductFeedAddresses.NewKey() : null;
        site.Put(settings);
        await siteService.UpdateSiteSettingsAsync(site);

        if (services.GetService<ProductFeedToolScope>() is { } scope)
        {
            scope.HasKey = action == "new";
        }

        return Json(new
        {
            hasKey = action == "new",
            settingsPage = GetProductFeedStatusTool.GetSettingsPath(services, services.GetService<IHttpContextAccessor>()?.HttpContext),
            message = action == "new"
                ? "A new access key was made. Every feed address given to a shopping channel stops working until it is replaced with the new address from the settings page."
                : "The access key was removed: anyone with the feed addresses can read the feed now.",
        });
    }
}
