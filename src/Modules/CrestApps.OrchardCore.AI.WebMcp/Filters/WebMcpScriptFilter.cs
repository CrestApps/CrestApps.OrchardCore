using System.Text.Json;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.AI.WebMcp.Filters;

/// <summary>
/// Adds the WebMCP script and its configuration to the full pages a signed-in user with the
/// <see cref="WebMcpPermissionsProvider.UseWebMcpTools"/> permission opens, on the page kinds the settings turn on.
/// </summary>
public sealed class WebMcpScriptFilter : IAsyncResultFilter
{
    private readonly WebMcpOptions _options;
    private readonly IAuthorizationService _authorizationService;
    private readonly IResourceManager _resourceManager;
    private readonly IAntiforgery _antiforgery;
    private readonly IEnumerable<IWebMcpClientConfigurationHandler> _handlers;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebMcpScriptFilter"/> class.
    /// </summary>
    /// <param name="options">The effective WebMCP settings.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="resourceManager">The resource manager.</param>
    /// <param name="antiforgery">The antiforgery service, which gives the token the page script sends.</param>
    /// <param name="handlers">The handlers that add scripts and settings to the page configuration.</param>
    public WebMcpScriptFilter(
        IOptionsSnapshot<WebMcpOptions> options,
        IAuthorizationService authorizationService,
        IResourceManager resourceManager,
        IAntiforgery antiforgery,
        IEnumerable<IWebMcpClientConfigurationHandler> handlers)
    {
        _options = options.Value;
        _authorizationService = authorizationService;
        _resourceManager = resourceManager;
        _antiforgery = antiforgery;
        _handlers = handlers;
    }

    /// <summary>
    /// Adds the script and its configuration to the page when the user and the page qualify.
    /// </summary>
    /// <param name="context">The result context.</param>
    /// <param name="next">The next step of the pipeline.</param>
    /// <returns>A task that completes when the result has run.</returns>
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (context.Result is not (ViewResult or PageResult))
        {
            await next();

            return;
        }

        var httpContext = context.HttpContext;
        var user = httpContext.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            await next();

            return;
        }

        var isAdminPage = AdminAttribute.IsApplied(httpContext);

        if (!_options.IsEnabledFor(isAdminPage) ||
            !await _authorizationService.AuthorizeAsync(user, WebMcpPermissionsProvider.UseWebMcpTools))
        {
            await next();

            return;
        }

        var configuration = new WebMcpClientConfigurationContext(httpContext, isAdminPage);
        var tokens = _antiforgery.GetAndStoreTokens(httpContext);
        var pathBase = httpContext.Request.PathBase.Value ?? string.Empty;

        configuration.Settings["toolsUrl"] = $"{pathBase}/{WebMcpConstants.ToolsRoute}";
        configuration.Settings["invokeUrlTemplate"] = $"{pathBase}/{WebMcpConstants.InvokeRoute}";
        configuration.Settings["antiforgeryHeaderName"] = tokens.HeaderName;
        configuration.Settings["antiforgeryToken"] = tokens.RequestToken;
        configuration.Settings["isAdmin"] = isAdminPage;

        foreach (var handler in _handlers)
        {
            await handler.ConfigureAsync(configuration);
        }

        // The block goes in the head so it is in the document before the foot scripts that read it run.
        _resourceManager.RegisterHeadScript(new HtmlString(BuildConfigurationElement(configuration.Settings)));
        _resourceManager.RegisterResource("script", WebMcpConstants.ResourceNames.Script).AtFoot();

        foreach (var script in configuration.Scripts)
        {
            _resourceManager.RegisterResource("script", script).AtFoot();
        }

        await next();
    }

    /// <summary>
    /// Builds the JSON block that carries the page configuration. The block is not run by the browser, and the
    /// serializer escapes the characters that could close it early.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The HTML of the block.</returns>
    internal static string BuildConfigurationElement(IDictionary<string, object> settings)
    {
        var json = JsonSerializer.Serialize(settings);

        return $"<script type=\"application/json\" id=\"{WebMcpConstants.ConfigurationElementId}\">{json}</script>";
    }
}
