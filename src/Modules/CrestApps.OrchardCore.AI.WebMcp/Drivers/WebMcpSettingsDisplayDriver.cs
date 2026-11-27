using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using CrestApps.OrchardCore.AI.WebMcp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.WebMcp.Drivers;

/// <summary>
/// Renders the WebMCP settings on the AI settings page: which tools and tool instances browser agents may call on
/// every page, which page kinds get the WebMCP scripts, and whether those pages get the page tools.
/// </summary>
internal sealed class WebMcpSettingsDisplayDriver : SiteDisplayDriver<WebMcpSettings>
{
    private readonly WebMcpToolSelectionService _toolSelection;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IShellReleaseManager _shellReleaseManager;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebMcpSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="toolSelection">The service that builds the tool pickers.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="shellReleaseManager">The shell release manager, which rebuilds the tenant after the settings change.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WebMcpSettingsDisplayDriver(
        WebMcpToolSelectionService toolSelection,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IShellReleaseManager shellReleaseManager,
        IStringLocalizer<WebMcpSettingsDisplayDriver> stringLocalizer)
    {
        _toolSelection = toolSelection;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _shellReleaseManager = shellReleaseManager;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(ISite site, WebMcpSettings settings, BuildEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext.User;

        if (!await _authorizationService.AuthorizeAsync(user, WebMcpPermissionsProvider.ManageWebMcpSettings))
        {
            return null;
        }

        var accessibleTools = await _toolSelection.GetAccessibleToolsAsync(user);
        var accessibleInstances = await _toolSelection.GetAccessibleInstancesAsync();
        var selected = settings.Tools ?? [];

        var results = new List<IDisplayResult>
        {
            Initialize<WebMcpSettingsViewModel>("WebMcpSettings_Edit", model =>
            {
                model.ExposeAllTools = settings.ExposeAllTools;
                model.EnableOnAdminPages = settings.EnableOnAdminPages;
                model.EnableOnFrontEndPages = settings.EnableOnFrontEndPages;
                model.EnablePageTools = settings.EnablePageTools;
                model.EnableStorefrontTools = settings.EnableStorefrontTools;
            }).Location(l => l.Zone("Content", "15").Card(S["Availability"], "10"))
            .OnGroup(SettingsGroupId),
            Initialize<WebMcpSettingsViewModel>("WebMcpSettingsTools_Edit", model =>
            {
                model.ExposeAllTools = settings.ExposeAllTools;
            }).Location(l => l.Zone("Content", "15.1").Card(S["Exposed Tools"], "20"))
            .OnGroup(SettingsGroupId),
        };

        if (accessibleTools.Count > 0)
        {
            results.Add(Initialize<EditProfileToolsViewModel>("EditProfileTools_Edit", model =>
            {
                _toolSelection.PopulateTools(model, accessibleTools, selected);
            }).Location(l => l.Zone("Content", "15.5").Card(S["Exposed Tools"], "20"))
            .OnGroup(SettingsGroupId));
        }

        if (accessibleInstances.Count > 0)
        {
            results.Add(Initialize<EditToolInstancesViewModel>("EditToolInstances_Edit", model =>
            {
                WebMcpToolSelectionService.PopulateInstances(model, accessibleInstances, selected);
            }).Location(l => l.Zone("Content", "15.6").Card(S["Exposed Tools"], "20"))
            .OnGroup(SettingsGroupId));
        }

        return Combine([.. results]);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, WebMcpSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext.User;

        if (!await _authorizationService.AuthorizeAsync(user, WebMcpPermissionsProvider.ManageWebMcpSettings))
        {
            return null;
        }

        var model = new WebMcpSettingsViewModel();
        var toolsModel = new EditProfileToolsViewModel();
        var instancesModel = new EditToolInstancesViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        await context.Updater.TryUpdateModelAsync(toolsModel, Prefix);
        await context.Updater.TryUpdateModelAsync(instancesModel, Prefix);

        var accessibleTools = await _toolSelection.GetAccessibleToolsAsync(user);
        var accessibleInstances = await _toolSelection.GetAccessibleInstancesAsync();

        var tools = WebMcpToolSelectionService.Merge(
            settings.Tools,
            accessibleTools.Keys,
            accessibleInstances.Select(instance => instance.Name),
            toolsModel,
            instancesModel);

        var changed = settings.ExposeAllTools != model.ExposeAllTools ||
            settings.EnableOnAdminPages != model.EnableOnAdminPages ||
            settings.EnableOnFrontEndPages != model.EnableOnFrontEndPages ||
            settings.EnablePageTools != model.EnablePageTools ||
            settings.EnableStorefrontTools != model.EnableStorefrontTools ||
            !(settings.Tools ?? []).OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(tools.OrderBy(name => name, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

        settings.ExposeAllTools = model.ExposeAllTools;
        settings.EnableOnAdminPages = model.EnableOnAdminPages;
        settings.EnableOnFrontEndPages = model.EnableOnFrontEndPages;
        settings.EnablePageTools = model.EnablePageTools;
        settings.EnableStorefrontTools = model.EnableStorefrontTools;
        settings.Tools = tools;

        if (changed && context.Updater.ModelState.IsValid)
        {
            // CrestApps.Core reads the tool selection through IOptionsMonitor, so the tenant is released to rebuild it.
            _shellReleaseManager.RequestRelease();
        }

        return await EditAsync(site, settings, context);
    }
}
