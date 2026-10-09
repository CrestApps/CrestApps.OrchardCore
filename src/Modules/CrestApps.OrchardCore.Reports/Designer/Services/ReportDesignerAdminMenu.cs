using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.Designer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Adds the report builder to the admin Reports menu: the list of designed reports, the list of views, and an item
/// for every designed report pinned to the menu, grouped under its category. Each pinned report is authorized with the
/// report as the resource, so it shows only to the people it is shared with.
/// </summary>
public sealed class ReportDesignerAdminMenu : AdminNavigationProvider
{
    private readonly ICatalog<ReportDesign> _designs;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDesignerAdminMenu"/> class.
    /// </summary>
    /// <param name="designs">The report catalog.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportDesignerAdminMenu(
        ICatalog<ReportDesign> designs,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<ReportDesignerAdminMenu> stringLocalizer)
    {
        _designs = designs;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        var designs = await _designs.GetAllAsync();
        var pinned = designs
            .Where(design => design.ShowInAdminMenu && !string.IsNullOrEmpty(design.DisplayText))
            .ToList();
        var user = _httpContextAccessor.HttpContext?.User;
        var canDesign = user is not null && await _authorizationService.AuthorizeAsync(user, ReportDesignerPermissions.ManageOwnReportDesigns);
        var canSeeList = canDesign;

        if (!canSeeList && user is not null)
        {
            foreach (var design in designs)
            {
                if (await _authorizationService.AuthorizeAsync(user, ReportDesignerPermissions.ViewAllReportDesigns, design))
                {
                    canSeeList = true;

                    break;
                }
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

                foreach (var group in pinned
                    .GroupBy(design => string.IsNullOrWhiteSpace(design.Category) ? string.Empty : design.Category.Trim(), StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(group => ReportsConstants.Categories.GetOrder(group.Key))
                    .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
                {
                    var categoryLabel = string.IsNullOrEmpty(group.Key)
                        ? S["Custom Reports"]
                        : new LocalizedString(group.Key, group.Key);

                    reports.Add(categoryLabel, categoryLabel.PrefixPosition(), category =>
                    {
                        category.AddClass("report-category");

                        foreach (var design in group.OrderBy(design => design.DisplayText, StringComparer.CurrentCultureIgnoreCase))
                        {
                            var label = new LocalizedString(design.DisplayText, design.DisplayText);

                            category.Add(label, label.PrefixPosition(), item => item
                                .AddClass("report")
                                .Action("Run", "ReportDesigns", new { area = ReportsConstants.Feature, id = design.ItemId })
                                .Permission(ReportDesignerPermissions.ViewAllReportDesigns)
                                .Resource(design)
                                .LocalNav());
                        }
                    });
                }
            }, priority: ReportsConstants.AdminMenuPriority);
    }
}
