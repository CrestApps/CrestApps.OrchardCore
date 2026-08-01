using CrestApps.OrchardCore.Reports.OpenXml.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.OpenXml;

/// <summary>
/// Registers the Open XML Excel export format for the Reports framework.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IReportExportFormat, ExcelReportExportFormat>();
    }
}
