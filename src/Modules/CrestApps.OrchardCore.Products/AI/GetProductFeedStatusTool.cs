using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Products.Core.Feeds;
using CrestApps.OrchardCore.Products.Feeds;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Products.AI;

/// <summary>
/// Reports what Settings › Commerce › Product feeds shows: the feed addresses, whether an access key and the site's base
/// URL are set, how many items the feed holds (<see cref="IProductFeedService.GetAsync"/>, built fresh as the page does)
/// and what Merchant Center would refuse. The access key is a secret: the addresses are returned without it, and the
/// settings page, which shows them in full, is linked. It needs <c>ManageProductFeeds</c>.
/// </summary>
internal sealed class GetProductFeedStatusTool : ProductsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getProductFeedStatus";

    private const int MaxProblems = 50;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {},
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns the product feed's XML and CSV addresses, whether an access key and the site base URL are set, how many items the feed holds, and the problems found (which product, what Merchant Center would refuse, whether it was left out). Use it to check a feed before submitting it to Google, Meta or TikTok. The access key is never shown.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ProductFeedPermissions.ManageProductFeeds;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var siteService = services.GetRequiredService<ISiteService>();
        var site = await siteService.GetSiteSettingsAsync();
        var settings = await siteService.GetSettingsAsync<ProductFeedSettings>();
        var hasKey = services.GetService<ProductFeedToolScope>()?.HasKey ?? !string.IsNullOrEmpty(settings.Key);
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var baseUrl = ProductFeedAddresses.GetBaseUrl(site, httpContext?.Request);
        var feed = await services.GetRequiredService<IProductFeedService>().GetAsync(currency: null, fresh: true, cancellationToken);
        var problems = feed.Problems ?? [];

        return Json(new
        {
            xmlAddress = baseUrl is null ? null : ProductFeedAddresses.Build(baseUrl, ProductFeedAddresses.XmlPath, null),
            csvAddress = baseUrl is null ? null : ProductFeedAddresses.Build(baseUrl, ProductFeedAddresses.CsvPath, null),
            hasKey,
            keyNote = hasKey
                ? "The feed needs its access key: the addresses work with ?key=… added. The full addresses are on the settings page."
                : "The feed has no access key, so anyone with the addresses can read it.",
            hasBaseUrl = !string.IsNullOrWhiteSpace(site.BaseUrl),
            baseUrlNote = string.IsNullOrWhiteSpace(site.BaseUrl)
                ? "The site has no base URL, so the addresses and product links follow whatever address the site was opened at. Set it under Settings › General."
                : null,
            items = feed.Items?.Count ?? 0,
            built = await LocalTimeAsync(services, feed.BuiltUtc),
            includesOutOfStock = settings.IncludeOutOfStock,
            problems = problems.Take(MaxProblems).Select(problem => new
            {
                itemId = problem.ItemId,
                title = problem.Title,
                problem = problem.Problem,
                leftOut = problem.LeftOut,
            }).ToArray(),
            problemCount = problems.Count,
            leftOut = problems.Count(problem => problem.LeftOut),
            truncated = problems.Count > MaxProblems,
            settingsPage = GetSettingsPath(services, httpContext),
        });
    }

    /// <summary>
    /// Gets the path of Settings › Commerce › Product feeds.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="httpContext">The current request, or <see langword="null"/>.</param>
    /// <returns>The path, or <see langword="null"/> when there is no request to build it from.</returns>
    internal static string GetSettingsPath(IServiceProvider services, HttpContext httpContext)
    {
        var linkGenerator = services.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        try
        {
            return linkGenerator.GetPathByName(httpContext, "AdminSettings", new { groupId = ProductFeedSettingsDisplayDriver.GroupId });
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
