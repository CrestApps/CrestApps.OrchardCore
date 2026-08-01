using System.IO;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.OpenXml.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Localization;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.OpenXml.Services;

public sealed class ExcelReportExportFormatTests
{
    [Fact]
    public void Serialize_WhenDocumentHasMultipleSections_ShouldCreateWorkbookWithExpectedSheetsAndCells()
    {
        // Arrange
        var document = new ReportDocument
        {
            Title = "Agent performance",
        }
            .Add(ReportSection.ForMetrics("Summary", [new ReportMetric("Open conversations", "42", "Current period")]))
            .Add(ReportSection.ForTable("Queues", [new ReportColumn("Queue"), new ReportColumn("Count")], [new ReportRow(["Support", "18"])]))
            .Add(ReportSection.ForBars("Channel mix", [new ReportBar("Voice", "12", 0.6)]))
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

        var exportFormat = new ExcelReportExportFormat(Mock.Of<IStringLocalizer<ExcelReportExportFormat>>());

        // Act
        var content = exportFormat.Serialize(document);

        // Assert
        using var stream = new MemoryStream(content);
        using var spreadsheetDocument = SpreadsheetDocument.Open(stream, false);
        var workbookPart = spreadsheetDocument.WorkbookPart;

        Assert.NotNull(workbookPart);

        var sheets = workbookPart.Workbook.Sheets.Elements<Sheet>().ToArray();
        Assert.Equal(4, sheets.Length);
        Assert.Equal("Agent performance 1", sheets[0].Name);
        Assert.Equal("Agent performance 2", sheets[1].Name);
        Assert.Equal("Agent performance 3", sheets[2].Name);
        Assert.Equal("Agent performance 4", sheets[3].Name);

        var summaryRows = GetSheetRows(workbookPart, sheets[0]);
        Assert.Equal(["Metric", "Value", "Hint"], GetCellValues(summaryRows[0]));
        Assert.Equal(["Open conversations", "42", "Current period"], GetCellValues(summaryRows[1]));

        var queueRows = GetSheetRows(workbookPart, sheets[1]);
        Assert.Equal(["Queue", "Count"], GetCellValues(queueRows[0]));
        Assert.Equal(["Support", "18"], GetCellValues(queueRows[1]));

        var barRows = GetSheetRows(workbookPart, sheets[2]);
        Assert.Equal(["Label", "Value", "Ratio"], GetCellValues(barRows[0]));
        Assert.Equal(["Voice", "12", "0.6"], GetCellValues(barRows[1]));

        var chartRows = GetSheetRows(workbookPart, sheets[3]);
        Assert.Equal(["Label", "Offered", "Answered"], GetCellValues(chartRows[0]));
        Assert.Equal(["Monday", "12", "10"], GetCellValues(chartRows[1]));
        Assert.Equal(["Tuesday", "18", "15"], GetCellValues(chartRows[2]));
    }

    [Fact]
    public void Serialize_WhenSectionNamesCollideOrContainInvalidCharacters_ShouldProduceValidUniqueSheetNames()
    {
        // Arrange
        var document = new ReportDocument()
            .Add(ReportSection.ForMetrics("Queue/Overview", [new ReportMetric("Calls", "8")]))
            .Add(ReportSection.ForMetrics("Queue:Overview", [new ReportMetric("Calls", "9")]));

        var exportFormat = new ExcelReportExportFormat(Mock.Of<IStringLocalizer<ExcelReportExportFormat>>());

        // Act
        var content = exportFormat.Serialize(document);

        // Assert
        using var stream = new MemoryStream(content);
        using var spreadsheetDocument = SpreadsheetDocument.Open(stream, false);
        var workbookPart = spreadsheetDocument.WorkbookPart;

        Assert.NotNull(workbookPart);

        var sheetNames = workbookPart.Workbook.Sheets.Elements<Sheet>().Select(sheet => sheet.Name.Value).ToArray();
        Assert.Equal(["Queue Overview", "Queue Overview (2)"], sheetNames);
    }

    [Fact]
    public void Serialize_WhenTableContainsSemanticTotals_ShouldRetainEveryRow()
    {
        // Arrange
        var document = new ReportDocument()
            .Add(ReportSection.ForTable(
                "Queues",
                [new ReportColumn("Queue"), new ReportColumn("Count")],
                [
                    new ReportRow(["Support", "2"]),
                    new ReportRow(["Customer care", "2"], ReportRowKind.Subtotal),
                    new ReportRow(["All queues", "2"], ReportRowKind.GrandTotal),
                ]));
        var exportFormat = new ExcelReportExportFormat(Mock.Of<IStringLocalizer<ExcelReportExportFormat>>());

        // Act
        var content = exportFormat.Serialize(document);

        // Assert
        using var stream = new MemoryStream(content);
        using var spreadsheetDocument = SpreadsheetDocument.Open(stream, false);
        var workbookPart = spreadsheetDocument.WorkbookPart;

        Assert.NotNull(workbookPart);

        var sheet = Assert.Single(workbookPart.Workbook.Sheets.Elements<Sheet>());
        var rows = GetSheetRows(workbookPart, sheet);

        Assert.Equal(["Queue", "Count"], GetCellValues(rows[0]));
        Assert.Equal(["Support", "2"], GetCellValues(rows[1]));
        Assert.Equal(["Customer care", "2"], GetCellValues(rows[2]));
        Assert.Equal(["All queues", "2"], GetCellValues(rows[3]));
    }

    private static Row[] GetSheetRows(WorkbookPart workbookPart, Sheet sheet)
    {
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id);
        return worksheetPart.Worksheet.GetFirstChild<SheetData>().Elements<Row>().ToArray();
    }

    private static string[] GetCellValues(Row row)
    {
        return [.. row.Elements<Cell>().Select(GetCellValue)];
    }

    private static string GetCellValue(Cell cell)
    {
        return cell.InlineString?.Text?.Text ?? cell.CellValue?.Text ?? cell.InnerText;
    }
}
