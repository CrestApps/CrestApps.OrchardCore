using System.Text;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using Microsoft.Extensions.Localization;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Core.Services;

public sealed class CsvReportExportFormatTests
{
    [Fact]
    public void Serialize_WhenDocumentContainsChart_ShouldWriteLabelsAndDatasets()
    {
        // Arrange
        var document = new ReportDocument()
            .Add(ReportSection.ForChart("Daily trend", new ReportChart
            {
                Type = ReportChartType.Line,
                Labels = ["Monday", "Tuesday"],
                Datasets =
                [
                    new ReportChartDataset("Offered", [12, 18]),
                    new ReportChartDataset("Answered", [10, 15]),
                ],
            }));
        var exportFormat = new CsvReportExportFormat(Mock.Of<IStringLocalizer<CsvReportExportFormat>>());

        // Act
        var content = Encoding.UTF8.GetString(exportFormat.Serialize(document));

        // Assert
        Assert.Equal("Label,Offered,Answered\r\nMonday,12,10\r\nTuesday,18,15\r\n", content.TrimStart('\uFEFF'));
    }
}
