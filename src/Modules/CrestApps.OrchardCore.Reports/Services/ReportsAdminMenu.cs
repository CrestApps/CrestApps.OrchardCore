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
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportsAdminMenu"/> class.
    /// </summary>
    /// <param name="reportManager">The report manager used to enumerate registered reports.</param>
    /// <param name="localizerFactory">The factory that localizes the names of the reports.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportsAdminMenu(
        IReportManager reportManager,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<ReportsAdminMenu> stringLocalizer)
    {
        _reportManager = reportManager;
        _localizerFactory = localizerFactory;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        var reports = _reportManager.GetReports();

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

                foreach (var group in reports
                    .GroupBy(report => report.Category)
                    .OrderBy(group => ReportsConstants.Categories.GetOrder(group.Key))
                    .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
                {
                    var categoryLabel = string.IsNullOrEmpty(group.Key)
                        ? S["General"]
                        : new LocalizedString(group.Key, group.Key);

                    reportsNode.Add(categoryLabel, categoryLabel.PrefixPosition(), categoryNode =>
                    {
                        categoryNode.AddClass("report-category");

                        foreach (var (report, displayName) in group
                            .Select(descriptor => (descriptor, _localizerFactory.Localize(descriptor.DisplayName)))
                            .OrderBy(entry => entry.Item2.Value, StringComparer.CurrentCultureIgnoreCase))
                        {
                            categoryNode.Add(displayName, displayName.PrefixPosition(), item => item
                                .AddClass("report")
                                .Action("Display", "Reports", new { area = ReportsConstants.Feature, id = report.Name })
                                .Permission(report.Permission)
                                .LocalNav());
                        }
                    });
                }
            }, priority: ReportsConstants.AdminMenuPriority);

        return ValueTask.CompletedTask;
    }
}
