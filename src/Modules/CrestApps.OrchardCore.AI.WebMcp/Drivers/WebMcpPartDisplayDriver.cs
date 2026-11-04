using System.Text.Json;
using CrestApps.Core.AI.WebMcp.Services;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using CrestApps.OrchardCore.AI.WebMcp.ViewModels;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.AI.WebMcp.Drivers;

/// <summary>
/// Lets editors pick, among the selectable tools they may use, the tools a content item exposes through WebMCP, and
/// adds those tools to the item's page for signed-in users who may use WebMCP.
/// </summary>
internal sealed class WebMcpPartDisplayDriver : ContentPartDisplayDriver<WebMcpPart>
{
    private readonly WebMcpToolSelectionService _toolSelection;
    private readonly IWebMcpAccessEvaluator _accessEvaluator;
    private readonly IAntiforgery _antiforgery;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IResourceManager _resourceManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebMcpPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="toolSelection">The service that builds the tool pickers.</param>
    /// <param name="accessEvaluator">The evaluator that decides whether a user may use WebMCP.</param>
    /// <param name="antiforgery">The antiforgery service, which gives the token the page script sends.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="resourceManager">The resource manager, which adds the scripts to the page.</param>
    public WebMcpPartDisplayDriver(
        WebMcpToolSelectionService toolSelection,
        IWebMcpAccessEvaluator accessEvaluator,
        IAntiforgery antiforgery,
        IHttpContextAccessor httpContextAccessor,
        IResourceManager resourceManager)
    {
        _toolSelection = toolSelection;
        _accessEvaluator = accessEvaluator;
        _antiforgery = antiforgery;
        _httpContextAccessor = httpContextAccessor;
        _resourceManager = resourceManager;
    }

    public override async Task<IDisplayResult> DisplayAsync(WebMcpPart part, BuildPartDisplayContext context)
    {
        // Only the item's own page (its Detail display) exposes its tools, not lists or summaries that show it.
        if (!string.Equals(context.DisplayType, "Detail", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;

        if (part.Tools is not { Count: > 0 } ||
            string.IsNullOrEmpty(part.ContentItem?.ContentItemId) ||
            user?.Identity?.IsAuthenticated != true ||
            !await _accessEvaluator.CanUseAsync(user, httpContext.RequestAborted))
        {
            return null;
        }

        var contentItemId = part.ContentItem.ContentItemId;
        var elementId = BuildElementId(contentItemId);

        // The scripts are registered with the resource manager rather than rendered by a shape, so they reach the page
        // whatever zones the theme's template for the content type renders.
        _resourceManager.RegisterHeadScript(new HtmlString(BuildConfigurationElement(elementId, BuildConfiguration(httpContext, contentItemId))));
        _resourceManager.RegisterResource("script", WebMcpConstants.ResourceNames.Script).AtFoot();
        _resourceManager.RegisterFootScript(new HtmlString(BuildRegistrationScript(elementId)));

        return null;
    }

    /// <summary>
    /// Builds the configuration the page script uses to list and call the content item's tools.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="contentItemId">The id of the content item.</param>
    /// <returns>The configuration.</returns>
    private Dictionary<string, object> BuildConfiguration(HttpContext httpContext, string contentItemId)
    {
        var pathBase = httpContext.Request.PathBase.Value ?? string.Empty;
        var escapedId = Uri.EscapeDataString(contentItemId);
        var tokens = _antiforgery.GetAndStoreTokens(httpContext);

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["toolsUrl"] = $"{pathBase}/{WebMcpConstants.ContentItemToolsRoute.Replace("{contentItemId}", escapedId, StringComparison.Ordinal)}",
            ["invokeUrlTemplate"] = $"{pathBase}/{WebMcpConstants.ContentItemInvokeRoute.Replace("{contentItemId}", escapedId, StringComparison.Ordinal)}",
            ["antiforgeryHeaderName"] = tokens.HeaderName,
            ["antiforgeryToken"] = tokens.RequestToken,
        };
    }

    /// <summary>
    /// Builds the id of the JSON block that carries a content item's configuration. Content item ids only hold
    /// letters and digits; anything else is dropped so the id is safe in HTML and JavaScript.
    /// </summary>
    /// <param name="contentItemId">The id of the content item.</param>
    /// <returns>The element id.</returns>
    internal static string BuildElementId(string contentItemId)
        => "crestapps-webmcp-content-" + new string((contentItemId ?? string.Empty).Where(char.IsAsciiLetterOrDigit).ToArray());

    /// <summary>
    /// Builds the JSON block that carries the configuration. The block is not run by the browser, and the serializer
    /// escapes the characters that could close it early.
    /// </summary>
    /// <param name="elementId">The id of the block.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The HTML of the block.</returns>
    internal static string BuildConfigurationElement(string elementId, IDictionary<string, object> configuration)
        => $"<script type=\"application/json\" id=\"{elementId}\">{JsonSerializer.Serialize(configuration)}</script>";

    /// <summary>
    /// Builds the script that registers the content item's tools with <c>document.modelContext</c> through
    /// CrestApps.Core's <c>registerServerTools</c>, which lists them from the item's endpoint.
    /// </summary>
    /// <param name="elementId">The id of the configuration block.</param>
    /// <returns>The HTML of the script.</returns>
    internal static string BuildRegistrationScript(string elementId)
        => "<script>(function () { var element = document.getElementById('" + elementId + "'); " +
            "if (!element || !document.modelContext || !window.CrestAppsWebMcp) { return; } " +
            "window.CrestAppsWebMcp.registerServerTools(document.modelContext, JSON.parse(element.textContent), window.fetch.bind(window)); })();</script>";

    public override async Task<IDisplayResult> EditAsync(WebMcpPart part, BuildPartEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext.User;
        var accessibleTools = await _toolSelection.GetAccessibleToolsAsync(user);
        var accessibleInstances = await _toolSelection.GetAccessibleInstancesAsync();
        var selected = part.Tools ?? [];

        var results = new List<IDisplayResult>
        {
            Initialize<WebMcpPartEditViewModel>(GetEditorShapeType(context), model =>
            {
                model.HasTools = accessibleTools.Count > 0 || accessibleInstances.Count > 0;
            }).Location("Content:10"),
        };

        if (accessibleTools.Count > 0)
        {
            results.Add(Initialize<EditProfileToolsViewModel>("EditProfileTools_Edit", model =>
            {
                _toolSelection.PopulateTools(model, accessibleTools, selected);
            }).Location("Content:10.1"));
        }

        if (accessibleInstances.Count > 0)
        {
            results.Add(Initialize<EditToolInstancesViewModel>("EditToolInstances_Edit", model =>
            {
                WebMcpToolSelectionService.PopulateInstances(model, accessibleInstances, selected);
            }).Location("Content:10.2"));
        }

        return Combine([.. results]);
    }

    public override async Task<IDisplayResult> UpdateAsync(WebMcpPart part, UpdatePartEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext.User;
        var toolsModel = new EditProfileToolsViewModel();
        var instancesModel = new EditToolInstancesViewModel();

        await context.Updater.TryUpdateModelAsync(toolsModel, Prefix);
        await context.Updater.TryUpdateModelAsync(instancesModel, Prefix);

        var accessibleTools = await _toolSelection.GetAccessibleToolsAsync(user);
        var accessibleInstances = await _toolSelection.GetAccessibleInstancesAsync();

        part.Tools = WebMcpToolSelectionService.Merge(
            part.Tools,
            accessibleTools.Keys,
            accessibleInstances.Select(instance => instance.Name),
            toolsModel,
            instancesModel);

        return await EditAsync(part, context);
    }
}
