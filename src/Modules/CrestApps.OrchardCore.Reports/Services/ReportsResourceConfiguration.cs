using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Reports.Services;

/// <summary>
/// Registers the scripts and styles of the Reports area as named resources.
/// </summary>
internal sealed class ReportsResourceConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    /// <summary>
    /// The name of the script that draws report charts. It renders every chart on the page when the page loads and
    /// exposes <c>window.CrestAppsReportCharts.render(container)</c> for content added later.
    /// </summary>
    public const string ChartsScript = "CrestApps.OrchardCore.Reports.Charts";

    /// <summary>
    /// The name of the report builder script.
    /// </summary>
    public const string DesignerScript = "CrestApps.OrchardCore.Reports.Builder";

    /// <summary>
    /// The name of the report builder style sheet.
    /// </summary>
    public const string DesignerStyle = "CrestApps.OrchardCore.Reports.Builder";

    /// <summary>
    /// The name of the style sheet of report pages: the containers that frame sections and the rows and marks people
    /// can click to filter other visuals.
    /// </summary>
    public const string DocumentStyle = "CrestApps.OrchardCore.Reports.Document";

    /// <summary>
    /// The name of the script that lets people click the rows and charts of a report to filter the visuals they are
    /// linked to.
    /// </summary>
    public const string InteractionsScript = "CrestApps.OrchardCore.Reports.Interactions";

    private static readonly ResourceManifest _manifest;

    static ReportsResourceConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript(ChartsScript)
            .SetUrl(
                "~/CrestApps.OrchardCore.Reports/scripts/report-charts.min.js",
                "~/CrestApps.OrchardCore.Reports/scripts/report-charts.js")
            .SetDependencies("chart.js")
            .SetVersion("1.0.0");

        _manifest
            .DefineScript(InteractionsScript)
            .SetUrl(
                "~/CrestApps.OrchardCore.Reports/scripts/report-interactions.min.js",
                "~/CrestApps.OrchardCore.Reports/scripts/report-interactions.js")
            .SetVersion("1.0.0");

        _manifest
            .DefineScript(DesignerScript)
            .SetUrl(
                "~/CrestApps.OrchardCore.Reports/scripts/report-designer.min.js",
                "~/CrestApps.OrchardCore.Reports/scripts/report-designer.js")
            .SetDependencies(ChartsScript, InteractionsScript)
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle(DocumentStyle)
            .SetUrl(
                "~/CrestApps.OrchardCore.Reports/styles/report-document.min.css",
                "~/CrestApps.OrchardCore.Reports/styles/report-document.css")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle(DesignerStyle)
            .SetUrl(
                "~/CrestApps.OrchardCore.Reports/styles/report-designer.min.css",
                "~/CrestApps.OrchardCore.Reports/styles/report-designer.css")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
