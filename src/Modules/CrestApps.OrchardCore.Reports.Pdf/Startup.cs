using CrestApps.OrchardCore.Reports.Pdf.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Pdf;

/// <summary>
/// Registers the PDF export format for the Reports framework.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddReportExportFormat<PdfReportExportFormat>();
    }
}
