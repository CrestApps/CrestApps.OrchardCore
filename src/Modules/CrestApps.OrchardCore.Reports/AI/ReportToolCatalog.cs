using System.Security.Claims;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Finds the report a tool names, built-in or designed: a built-in report by its technical name, then, when the Report
/// Builder is on, a designed report by its id.
/// </summary>
internal static class ReportToolCatalog
{
    /// <summary>
    /// The kind of a built-in report, which a module registers in code.
    /// </summary>
    public const string BuiltIn = "builtIn";

    /// <summary>
    /// The kind of a report designed in the Report Builder.
    /// </summary>
    public const string Designed = "designed";

    /// <summary>
    /// Gets the answer when no report a person may open has the id.
    /// </summary>
    /// <param name="id">The id that was given.</param>
    /// <returns>The answer.</returns>
    public static string NotFoundMessage(string id)
        => $"No report you may open has the id '{id}'. Call listReports to find it.";

    /// <summary>
    /// Finds a built-in report.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="id">The report's technical name.</param>
    /// <returns>The report, or <see langword="null"/>.</returns>
    public static ReportDescriptor FindBuiltIn(IServiceProvider services, string id)
        => string.IsNullOrEmpty(id) ? null : services.GetService<IReportManager>()?.GetDescriptor(id);

    /// <summary>
    /// Finds a designed report, when the Report Builder is on.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="id">The report's id.</param>
    /// <returns>The report, or <see langword="null"/> when there is none or the Report Builder is off.</returns>
    public static async Task<ReportDesign> FindDesignedAsync(IServiceProvider services, string id)
    {
        var designService = services.GetService<ReportDesignService>();

        return designService is null || string.IsNullOrEmpty(id) ? null : await designService.FindAsync(id);
    }

    /// <summary>
    /// Whether the caller may open a designed report: the permission the report page checks, with the report as the
    /// resource, which its owner and the people and roles it is shared with hold.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="user">The caller.</param>
    /// <param name="design">The report.</param>
    /// <returns><see langword="true"/> when the caller may open it.</returns>
    public static async Task<bool> CanViewAsync(IServiceProvider services, ClaimsPrincipal user, ReportDesign design)
        => await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, ReportDesignerPermissions.ViewAllReportDesigns, design);

    /// <summary>
    /// Whether the caller may change a designed report: the permission the designer checks, with the report as the
    /// resource.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="user">The caller.</param>
    /// <param name="design">The report.</param>
    /// <returns><see langword="true"/> when the caller may change it.</returns>
    public static async Task<bool> CanManageAsync(IServiceProvider services, ClaimsPrincipal user, ReportDesign design)
        => await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, ReportDesignerPermissions.ManageAllReportDesigns, design);

    /// <summary>
    /// Whether the caller may export a built-in report: its export permission, when it has one, beside the one that
    /// views it.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="user">The caller.</param>
    /// <param name="report">The report.</param>
    /// <returns><see langword="true"/> when the caller may export it.</returns>
    public static async Task<bool> CanExportAsync(IServiceProvider services, ClaimsPrincipal user, ReportDescriptor report)
        => report.ExportPermission is null || await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, report.ExportPermission);

    /// <summary>
    /// Whether the caller may export a designed report: people it is shared with when it allows export, and the people
    /// who may change it.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="user">The caller.</param>
    /// <param name="design">The report.</param>
    /// <returns><see langword="true"/> when the caller may export it.</returns>
    public static async Task<bool> CanExportAsync(IServiceProvider services, ClaimsPrincipal user, ReportDesign design)
        => design.AllowExport || await CanManageAsync(services, user, design);

    /// <summary>
    /// Lists the exposed filters of a designed report, one per id: the filters its viewers can change.
    /// </summary>
    /// <param name="design">The report.</param>
    /// <returns>The filters.</returns>
    public static IReadOnlyList<ReportFilterDefinition> GetExposedFilters(ReportDesign design)
        => design.GetFilters()
            .Where(filter => filter.Exposed && !string.IsNullOrEmpty(filter.Id))
            .GroupBy(filter => filter.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

    /// <summary>
    /// Whether a filter takes a range: a lower and an upper bound.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns><see langword="true"/> for a range.</returns>
    public static bool IsRange(ReportFilterDefinition filter)
        => filter.Control is ReportFilterControl.DateRange or ReportFilterControl.NumberRange ||
            filter.Operator == global::OrchardCore.DataSources.DataFilterOperator.Between;
}
