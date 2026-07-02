using CrestApps.OrchardCore.Reports.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Reports.Services;

/// <summary>
/// Builds the top-level admin Reports menu from the registered reports, grouped by category. Each report
/// entry links to the shared report page and is gated by the report's own permission.
/// </summary>
public sealed class ReportsAdminMenu : AdminNavigationProvider
{
    private readonly IReportManager _reportManager;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportsAdminMenu"/> class.
    /// </summary>
    /// <param name="reportManager">The report manager used to enumerate registered reports.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportsAdminMenu(
        IReportManager reportManager,
        IStringLocalizer<ReportsAdminMenu> stringLocalizer)
    {
        _reportManager = reportManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        var reports = _reportManager.ListReports();

        if (reports.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        builder
            .Add(S["Reports"], "after.40", reportsNode =>
            {
                reportsNode
                    .AddClass("reports")
                    .Id("reports");

                foreach (var group in reports.GroupBy(report => report.Category ?? string.Empty))
                {
                    var categoryLabel = string.IsNullOrEmpty(group.Key)
                        ? S["General"]
                        : new LocalizedString(group.Key, group.Key);

                    reportsNode.Add(categoryLabel, categoryLabel.PrefixPosition(), categoryNode =>
                    {
                        categoryNode.AddClass("report-category");

                        foreach (var report in group)
                        {
                            categoryNode.Add(report.DisplayName, report.DisplayName.PrefixPosition(), item => item
                                .AddClass("report")
                                .Action("Display", "Reports", new { area = ReportsConstants.Feature, id = report.Name })
                                .Permission(report.Permission)
                                .LocalNav());
                        }
                    });
                }
            }, priority: 1);

        return ValueTask.CompletedTask;
    }
}
