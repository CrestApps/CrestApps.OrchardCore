using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Tools.Services;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using CrestApps.OrchardCore.AI.WebMcp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.WebMcp.Drivers;

/// <summary>
/// Renders the WebMCP settings on the AI settings page: which tools and tool instances browser agents may call, and
/// which page kinds offer them.
/// </summary>
public sealed class WebMcpSettingsDisplayDriver : SiteDisplayDriver<WebMcpSettings>
{
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly IAIToolInstanceAccessor _instanceAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAIToolAccessEvaluator _toolAccessEvaluator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebMcpSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="toolDefinitions">The registered AI tool definitions.</param>
    /// <param name="instanceAccessor">The accessor of the tool instances the current user may assign.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="toolAccessEvaluator">The evaluator of the per-tool permission.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WebMcpSettingsDisplayDriver(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IAIToolInstanceAccessor instanceAccessor,
        IAuthorizationService authorizationService,
        IAIToolAccessEvaluator toolAccessEvaluator,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<WebMcpSettingsDisplayDriver> stringLocalizer)
    {
        _toolDefinitions = toolDefinitions.Value;
        _instanceAccessor = instanceAccessor;
        _authorizationService = authorizationService;
        _toolAccessEvaluator = toolAccessEvaluator;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(ISite site, WebMcpSettings settings, BuildEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, WebMcpPermissionsProvider.ManageWebMcpSettings))
        {
            return null;
        }

        var accessibleTools = await GetAccessibleToolsAsync();
        var accessibleInstances = await _instanceAccessor.GetAccessibleInstancesAsync();
        var selected = settings.Tools ?? [];

        var results = new List<IDisplayResult>
        {
            Initialize<WebMcpSettingsViewModel>("WebMcpSettings_Edit", model =>
            {
                model.ExposeAllTools = settings.ExposeAllTools;
                model.EnableOnAdminPages = settings.EnableOnAdminPages;
                model.EnableOnFrontEndPages = settings.EnableOnFrontEndPages;
            }).Location("Content:15%WebMCP;1")
            .OnGroup(SettingsGroupId),
        };

        if (accessibleTools.Count > 0)
        {
            results.Add(Initialize<EditProfileToolsViewModel>("EditProfileTools_Edit", model =>
            {
                model.Tools = accessibleTools
                    .GroupBy(tool => tool.Value.Category ?? S["Miscellaneous"])
                    .OrderBy(group => group.Key)
                    .ToDictionary(group => group.Key, group => group.Select(entry => new ToolEntry
                    {
                        ItemId = entry.Key,
                        DisplayText = entry.Value.Title,
                        Description = entry.Value.Description,
                        IsSelected = selected.Contains(entry.Key, StringComparer.OrdinalIgnoreCase),
                    }).OrderBy(entry => entry.DisplayText).ToArray());
            }).Location("Content:15%WebMCP;5")
            .OnGroup(SettingsGroupId));
        }

        if (accessibleInstances.Count > 0)
        {
            results.Add(Initialize<EditToolInstancesViewModel>("EditToolInstances_Edit", model =>
            {
                model.Instances = accessibleInstances
                    .Select(instance => new ToolInstanceEntry
                    {
                        Name = instance.Name,
                        Description = instance.Description,
                        IsSelected = selected.Contains(instance.Name, StringComparer.OrdinalIgnoreCase),
                    })
                    .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }).Location("Content:15%WebMCP;10")
            .OnGroup(SettingsGroupId));
        }

        return Combine([.. results]);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, WebMcpSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, WebMcpPermissionsProvider.ManageWebMcpSettings))
        {
            return null;
        }

        var model = new WebMcpSettingsViewModel();
        var toolsModel = new EditProfileToolsViewModel();
        var instancesModel = new EditToolInstancesViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        await context.Updater.TryUpdateModelAsync(toolsModel, Prefix);
        await context.Updater.TryUpdateModelAsync(instancesModel, Prefix);

        var accessibleTools = await GetAccessibleToolsAsync();
        var accessibleInstances = await _instanceAccessor.GetAccessibleInstancesAsync();

        var selectedToolKeys = toolsModel.Tools?.Values?
            .SelectMany(entries => entries)
            .Where(entry => entry.IsSelected)
            .Select(entry => entry.ItemId) ?? [];

        var selectedInstanceNames = instancesModel.Instances?
            .Where(entry => entry.IsSelected)
            .Select(entry => entry.Name) ?? [];

        // A user only changes the tools they may assign; the tools they may not see stay as they were.
        var tools = (settings.Tools ?? [])
            .Where(name => !accessibleTools.ContainsKey(name) &&
                !accessibleInstances.Any(instance => string.Equals(instance.Name, name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        tools.AddRange(accessibleTools.Keys.Intersect(selectedToolKeys, StringComparer.OrdinalIgnoreCase));
        tools.AddRange(accessibleInstances
            .Select(instance => instance.Name)
            .Intersect(selectedInstanceNames, StringComparer.OrdinalIgnoreCase));

        settings.ExposeAllTools = model.ExposeAllTools;
        settings.EnableOnAdminPages = model.EnableOnAdminPages;
        settings.EnableOnFrontEndPages = model.EnableOnFrontEndPages;
        settings.Tools = tools;

        return await EditAsync(site, settings, context);
    }

    private async Task<Dictionary<string, AIToolDefinitionEntry>> GetAccessibleToolsAsync()
    {
        var accessibleTools = new Dictionary<string, AIToolDefinitionEntry>(StringComparer.OrdinalIgnoreCase);
        var user = _httpContextAccessor.HttpContext.User;

        foreach (var tool in _toolDefinitions.GetSelectableTools())
        {
            if (await _toolAccessEvaluator.IsAuthorizedAsync(user, tool.Key))
            {
                accessibleTools[tool.Key] = tool.Value;
            }
        }

        return accessibleTools;
    }
}
