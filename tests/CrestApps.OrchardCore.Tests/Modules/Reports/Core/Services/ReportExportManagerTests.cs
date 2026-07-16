using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Core.Services;

public sealed class ReportExportManagerTests
{
    [Fact]
    public void Constructor_WhenFormatNamesDifferOnlyByCase_ShouldThrow()
    {
        // Arrange
        IReportExportFormat[] formats =
        [
            new TestExportFormat("sample"),
            new TestExportFormat("SAMPLE"),
        ];

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => new ReportExportManager(formats));

        // Assert
        Assert.Contains("sample", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestExportFormat : IReportExportFormat
    {
        public TestExportFormat(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public LocalizedString DisplayName => new(Name, Name);

        public string ContentType => "application/octet-stream";

        public string FileExtension => "bin";

        public byte[] Serialize(ReportDocument document)
        {
            return [];
        }
    }
}
