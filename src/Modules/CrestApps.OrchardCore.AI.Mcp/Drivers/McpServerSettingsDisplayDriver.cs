using CrestApps.Core;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Models;
using CrestApps.OrchardCore.AI.Mcp.Services;
using CrestApps.OrchardCore.AI.Mcp.ViewModels;
using CrestApps.OrchardCore.AI.Tools.Services;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Mcp.Drivers;

/// <summary>
/// Display driver that renders the MCP server settings on the site settings page, including the
/// authentication configuration and the opt-in selection of tools and tool instances exposed to MCP clients.
/// </summary>
public sealed class McpServerSettingsDisplayDriver : SiteDisplayDriver<McpServerSettings>
{
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly IAIToolInstanceAccessor _instanceAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IShellReleaseManager _shellReleaseManager;

    internal readonly IStringLocalizer S;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpServerSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="toolDefinitions">The registered AI tool definitions.</param>
    /// <param name="instanceAccessor">The accessor that resolves the tool instances the current user may assign.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="shellReleaseManager">The shell release manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public McpServerSettingsDisplayDriver(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IAIToolInstanceAccessor instanceAccessor,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IShellReleaseManager shellReleaseManager,
        IStringLocalizer<McpServerSettingsDisplayDriver> stringLocalizer)
    {
        _toolDefinitions = toolDefinitions.Value;
        _instanceAccessor = instanceAccessor;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _shellReleaseManager = shellReleaseManager;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(ISite site, McpServerSettings settings, BuildEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, McpServerPermissionsProvider.ManageMcpServerSettings))
        {
            return null;
        }

        context.AddTenantReloadWarningWrapper();

        var accessibleTools = await GetAccessibleToolsAsync();
        var accessibleInstances = await _instanceAccessor.GetAccessibleInstancesAsync();

        var results = new List<IDisplayResult>
        {
            Initialize<McpServerSettingsViewModel>("McpServerSettings_Edit", model =>
            {
                model.AuthenticationType = settings.AuthenticationType;
                model.RequireAccessPermission = settings.RequireAccessPermission;
                model.ExposeAllTools = settings.ExposeAllTools;
                model.HasApiKey = !string.IsNullOrEmpty(settings.ApiKey);
                model.AuthenticationTypes =
                [
                    new SelectListItem(S["OpenID Connect"], nameof(McpServerAuthenticationType.OpenId)),
                    new SelectListItem(S["API key"], nameof(McpServerAuthenticationType.ApiKey)),
                    new SelectListItem(S["None (development only)"], nameof(McpServerAuthenticationType.None)),
                ];
            }).Location("Content:1%MCP Server;1")
            .OnGroup(SettingsGroupId),
        };

        if (accessibleTools.Count > 0)
        {
            results.Add(Initialize<EditProfileToolsViewModel>("EditProfileTools_Edit", model =>
            {
                var selected = settings.Tools ?? [];

                model.Tools = accessibleTools
                    .GroupBy(tool => tool.Value.Category ?? S["Miscellaneous"])
                    .OrderBy(group => group.Key)
                    .ToDictionary(group => group.Key, group => group.Select(entry => new ToolEntry
                    {
                        ItemId = entry.Key,
                        DisplayText = entry.Value.Title,
                        Description = entry.Value.Description,
                        IsSelected = selected.Contains(entry.Key),
                    }).OrderBy(entry => entry.DisplayText).ToArray());
            }).Location("Content:1%MCP Server;5")
            .OnGroup(SettingsGroupId));
        }

        if (accessibleInstances.Count > 0)
        {
            results.Add(Initialize<EditToolInstancesViewModel>("EditToolInstances_Edit", model =>
            {
                var selected = settings.Tools ?? [];

                model.Instances = accessibleInstances
                    .Select(instance => new ToolInstanceEntry
                    {
                        Name = instance.Name,
                        Description = instance.Description,
                        IsSelected = selected.Contains(instance.Name, StringComparer.OrdinalIgnoreCase),
                    })
                    .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }).Location("Content:1%MCP Server;10")
            .OnGroup(SettingsGroupId));
        }

        return Combine([.. results]);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, McpServerSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, McpServerPermissionsProvider.ManageMcpServerSettings))
        {
            return null;
        }

        var model = new McpServerSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var toolsModel = new EditProfileToolsViewModel();
        var instancesModel = new EditToolInstancesViewModel();

        await context.Updater.TryUpdateModelAsync(toolsModel, Prefix);
        await context.Updater.TryUpdateModelAsync(instancesModel, Prefix);

        if (model.AuthenticationType == McpServerAuthenticationType.ApiKey && string.IsNullOrWhiteSpace(model.ApiKey) && string.IsNullOrEmpty(settings.ApiKey))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiKey), S["The API key is required when the API key authentication type is selected."]);
        }

        if (!context.Updater.ModelState.IsValid)
        {
            return await EditAsync(site, settings, context);
        }

        var accessibleTools = await GetAccessibleToolsAsync();
        var accessibleInstances = await _instanceAccessor.GetAccessibleInstancesAsync();

        var selectedToolKeys = toolsModel.Tools?.Values?
            .SelectMany(entries => entries)
            .Where(entry => entry.IsSelected)
            .Select(entry => entry.ItemId) ?? [];

        var selectedInstanceNames = instancesModel.Instances?
            .Where(entry => entry.IsSelected)
            .Select(entry => entry.Name) ?? [];

        var tools = new List<string>();

        tools.AddRange(accessibleTools.Keys.Intersect(selectedToolKeys, StringComparer.Ordinal));
        tools.AddRange(accessibleInstances
            .Select(instance => instance.Name)
            .Intersect(selectedInstanceNames, StringComparer.OrdinalIgnoreCase));

        var apiKey = string.IsNullOrWhiteSpace(model.ApiKey)
            ? settings.ApiKey
            : model.ApiKey.Trim();

        settings.AuthenticationType = model.AuthenticationType;
        settings.ApiKey = apiKey;
        settings.RequireAccessPermission = model.RequireAccessPermission;
        settings.ExposeAllTools = model.ExposeAllTools;
        settings.Tools = tools;

        _shellReleaseManager.RequestRelease();

        return await EditAsync(site, settings, context);
    }

    private async Task<Dictionary<string, AIToolDefinitionEntry>> GetAccessibleToolsAsync()
    {
        var accessibleTools = new Dictionary<string, AIToolDefinitionEntry>();

        if (_toolDefinitions.Tools.Count == 0)
        {
            return accessibleTools;
        }

        var user = _httpContextAccessor.HttpContext.User;

        foreach (var tool in _toolDefinitions.GetSelectableTools())
        {
            if (await _authorizationService.AuthorizeAsync(user, AIPermissions.AccessAITool, tool.Key as object))
            {
                accessibleTools[tool.Key] = tool.Value;
            }
        }

        return accessibleTools;
    }
}
