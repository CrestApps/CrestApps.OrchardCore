using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using CrestApps.OrchardCore.TenantHierarchy.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.TenantHierarchy.Controllers;

/// <summary>
/// Shows the hierarchy activity log of a parent: who created, changed, removed or entered which child tenant, and when.
/// </summary>
[Admin]
[Feature(TenantHierarchyConstants.Features.Parent)]
public sealed class ActivityController : Controller
{
    private readonly HierarchyAuditLog _auditLog;
    private readonly ChildTenantEntryStore _entryStore;
    private readonly HierarchyLabelsProvider _labelsProvider;
    private readonly IAuthorizationService _authorizationService;
    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityController"/> class.
    /// </summary>
    /// <param name="auditLog">The hierarchy activity log.</param>
    /// <param name="entryStore">The registry store.</param>
    /// <param name="labelsProvider">The labels provider.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="shellSettings">The settings of the parent tenant.</param>
    public ActivityController(
        HierarchyAuditLog auditLog,
        ChildTenantEntryStore entryStore,
        HierarchyLabelsProvider labelsProvider,
        IAuthorizationService authorizationService,
        ShellSettings shellSettings)
    {
        _auditLog = auditLog;
        _entryStore = entryStore;
        _labelsProvider = labelsProvider;
        _authorizationService = authorizationService;
        _shellSettings = shellSettings;
    }

    /// <summary>
    /// Shows a page of the activity log, optionally for one child tenant.
    /// </summary>
    /// <param name="child">The registry entry to filter by, or <see langword="null"/>.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    [Admin("children/activity", "TenantHierarchyActivity")]
    public async Task<IActionResult> Index(
        string child,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!_shellSettings.IsParentTenant() || !await _authorizationService.AuthorizeAsync(User, TenantHierarchyPermissions.ViewChildTenants))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var total = await _auditLog.CountAsync(child);
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(child))
        {
            routeData.Values.TryAdd("child", child);
        }

        return View(new HierarchyActivityViewModel
        {
            Events = (await _auditLog.PageAsync(child, pager.GetStartIndex(), pager.PageSize)).ToList(),
            Child = string.IsNullOrEmpty(child) ? null : await _entryStore.FindByEntryIdAsync(child),
            Pager = await shapeFactory.PagerAsync(pager, total, routeData),
            Labels = _labelsProvider.GetLabels(),
        });
    }
}
