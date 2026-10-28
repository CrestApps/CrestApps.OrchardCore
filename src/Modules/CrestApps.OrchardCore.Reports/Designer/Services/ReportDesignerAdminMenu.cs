using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.Designer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Adds the report builder to the admin Reports menu: the list of designed reports, the list of views, the list of
/// report groups, and an item for every designed report pinned to the menu, under the (nested) item of its group, or
/// directly under Reports when it is in no group. Each pinned report is authorized with the report as the resource, so
/// it shows only to the people it is shared with, and a group shows only when it leads to such a report.
/// </summary>
public sealed class ReportDesignerAdminMenu : AdminNavigationProvider
{
    // Groups come after the report builder's own pages, and a group's reports after its nested groups.
    private const int GroupsPosition = 10;
    private const int ReportsPosition = 20;

    private readonly ICatalog<ReportDesign> _designs;
    private readonly ICatalog<ReportGroup> _groups;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDesignerAdminMenu"/> class.
    /// </summary>
    /// <param name="designs">The report catalog.</param>
    /// <param name="groups">The report group catalog.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportDesignerAdminMenu(
        ICatalog<ReportDesign> designs,
        ICatalog<ReportGroup> groups,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<ReportDesignerAdminMenu> stringLocalizer)
    {
        _designs = designs;
        _groups = groups;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        var designs = await _designs.GetAllAsync();
        var user = _httpContextAccessor.HttpContext?.User;
        var canDesign = user is not null && await _authorizationService.AuthorizeAsync(user, ReportDesignerPermissions.ManageOwnReportDesigns);
        var canManageGroups = user is not null && await _authorizationService.AuthorizeAsync(user, ReportDesignerPermissions.ManageReportGroups);
        var canSeeList = canDesign;

        // Only the pinned reports the user may run are added, so a group shows only when it leads to one of them.
        var pinned = new List<ReportDesign>();

        if (user is not null)
        {
            foreach (var design in designs)
            {
                var canView = await _authorizationService.AuthorizeAsync(user, ReportDesignerPermissions.ViewAllReportDesigns, design);

                canSeeList |= canView;

                if (canView && design.ShowInAdminMenu && !string.IsNullOrEmpty(design.DisplayText))
                {
                    pinned.Add(design);
                }
            }
        }

        var tree = new ReportGroupTree(pinned.Count == 0 ? [] : await _groups.GetAllAsync());

        // A report whose group was deleted is listed as a report in no group.
        var byGroup = pinned.ToLookup(design => tree.Find(design.GroupId)?.ItemId ?? string.Empty, StringComparer.Ordinal);
        var shownGroups = new HashSet<string>(StringComparer.Ordinal);

        foreach (var groupId in byGroup.Select(reports => reports.Key).Where(groupId => groupId.Length > 0))
        {
            foreach (var group in tree.GetPath(groupId))
            {
                shownGroups.Add(group.ItemId);
            }
        }

        builder
            .Add(S["Reports"], "after.40", reports =>
            {
                reports
                    .AddClass("reports")
                    .Id("reports");

                if (canSeeList)
                {
                    reports.Add(canDesign ? S["Report Builder"] : S["Shared Reports"], "1", designer => designer
                        .AddClass("report-designer")
                        .Id("reportDesigner")
                        .Action("Index", "ReportDesigns", new { area = ReportsConstants.Feature })
                        .LocalNav());
                }

                if (canDesign)
                {
                    reports.Add(S["Report Views"], "2", views => views
                        .AddClass("report-views")
                        .Id("reportViews")
                        .Action("Index", "ReportViews", new { area = ReportsConstants.Feature })
                        .Permission(ReportDesignerPermissions.ManageOwnReportDesigns)
                        .LocalNav());
                }

                if (canManageGroups)
                {
                    reports.Add(S["Report Groups"], "3", groups => groups
                        .AddClass("report-groups")
                        .Id("reportGroups")
                        .Action("Index", "ReportGroups", new { area = ReportsConstants.Feature })
                        .Permission(ReportDesignerPermissions.ManageReportGroups)
                        .LocalNav());
                }

                AddGroupContents(reports, null, tree, byGroup, shownGroups);
            }, priority: ReportsConstants.AdminMenuPriority);
    }

    // Adds a group's content under its menu item, or the top-level content under Reports: first the nested groups that
    // lead to a pinned report, in their display order, then the reports, by title.
    private static void AddGroupContents(
        NavigationItemBuilder parent,
        string groupId,
        ReportGroupTree tree,
        ILookup<string, ReportDesign> byGroup,
        HashSet<string> shownGroups)
    {
        var position = 0;

        foreach (var group in tree.GetChildren(groupId).Where(group => shownGroups.Contains(group.ItemId)))
        {
            var label = new LocalizedString(group.DisplayText, group.DisplayText);

            parent.Add(label, $"{GroupsPosition}.{++position}", item =>
            {
                item.AddClass("report-group");
                AddGroupContents(item, group.ItemId, tree, byGroup, shownGroups);
            });
        }

        position = 0;

        foreach (var design in byGroup[groupId ?? string.Empty].OrderBy(design => design.DisplayText, StringComparer.CurrentCultureIgnoreCase))
        {
            var label = new LocalizedString(design.DisplayText, design.DisplayText);

            parent.Add(label, $"{ReportsPosition}.{++position}", item => item
                .AddClass("report")
                .Action("Run", "ReportDesigns", new { area = ReportsConstants.Feature, id = design.ItemId })
                .Permission(ReportDesignerPermissions.ViewAllReportDesigns)
                .Resource(design)
                .LocalNav());
        }
    }
}
