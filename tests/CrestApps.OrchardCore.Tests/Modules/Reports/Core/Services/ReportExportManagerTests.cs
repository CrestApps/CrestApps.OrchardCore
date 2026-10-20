using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Core.Services;

public sealed class ReportExportManagerTests
{
    [Fact]
    public void GetFormats_RegisteredFormats_AreListedWithoutBeingCreated()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddReportExportFormat<CsvReportExportFormat>();
        services.AddReportExportFormat<SampleExportFormat>();
        using var provider = services.BuildServiceProvider();
        var manager = new ReportExportManager(provider.GetRequiredService<IOptions<ReportExportFormatOptions>>(), provider);

        // Act
        var formats = manager.GetFormats();

        // Assert
        Assert.Equal([ReportsConstants.CsvExportFormat, "sample"], formats.Select(format => format.Name));
        Assert.Equal(".bin", manager.GetDescriptor("SAMPLE").Extension);
        Assert.Equal(0, SampleExportFormat.Instances);
        Assert.IsType<SampleExportFormat>(manager.GetFormat("sample"));
        Assert.Null(manager.GetFormat("pdf"));
    }

    [Fact]
    public void Constructor_WhenFormatNamesDifferOnlyByCase_ShouldThrow()
    {
        // Arrange
        var options = new ReportExportFormatOptions()
            .Add(new ReportExportFormatDescriptor("sample", LocalizationSource.Create("Sample"), ".bin", "application/octet-stream", typeof(SampleExportFormat)))
            .Add(new ReportExportFormatDescriptor("SAMPLE", LocalizationSource.Create("Sample"), ".bin", "application/octet-stream", typeof(CsvReportExportFormat)));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => new ReportExportManager(Options.Create(options), new ServiceCollection().BuildServiceProvider()));

        // Assert
        Assert.Contains("sample", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class SampleExportFormat : IReportExportFormat
    {
        public SampleExportFormat()
        {
            Instances++;
        }

        public static int Instances { get; private set; }

        public static string Name => "sample";

        public static LocalizationSource DisplayName { get; } = LocalizationSource.Create("Sample");

        public static string Extension => ".bin";

        public static string ContentType => "application/octet-stream";

        public byte[] Serialize(ReportDocument document)
        {
            return [];
        }
    }
}
