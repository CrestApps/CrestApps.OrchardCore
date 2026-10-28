using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.OpenXml.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

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

        var exportFormat = new ExcelReportExportFormat();

        // Act
        var content = exportFormat.Serialize(document);

        // Assert
        using var stream = new MemoryStream(content);
        using var spreadsheetDocument = SpreadsheetDocument.Open(stream, false);
        var workbookPart = spreadsheetDocument.WorkbookPart;

        Assert.NotNull(workbookPart);

        var sheets = workbookPart.Workbook.Sheets.Elements<Sheet>().ToArray();
        Assert.Equal(4, sheets.Length);
        Assert.Equal("Summary", sheets[0].Name);
        Assert.Equal("Queues", sheets[1].Name);
        Assert.Equal("Channel mix", sheets[2].Name);
        Assert.Equal("Daily trend", sheets[3].Name);

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
        Assert.Equal(CellValues.Number, chartRows[1].Elements<Cell>().ElementAt(1).DataType.Value);
    }

    [Fact]
    public void Serialize_WhenSectionsAreInContainers_ShouldWriteEachContainerToAWorksheetNamedAfterIt()
    {
        // Arrange
        var authors = ReportSection.ForTable("Authors", [new ReportColumn("Author")], [new ReportRow(["Alice"]) { Key = "alice" }, new ReportRow(["Bob"]) { Key = "bob" }]);
        authors.Id = "v1";
        authors.ContainerId = "k1";
        authors.Selection = new ReportSectionSelection { TargetIds = ["v2", "v3"], SelectedKeys = ["alice"], SelectedLabels = ["Alice"] };

        var posts = ReportSection.ForTable("Posts", [new ReportColumn("Title")], [new ReportRow(["Post 1"])]);
        posts.Id = "v2";
        posts.ContainerId = "k2";

        var trend = ReportSection.ForChart("Trend", new ReportChart { Labels = ["Monday"], Datasets = [new ReportChartDataset("Posts", [3])] });
        trend.Id = "v3";
        trend.ContainerId = "k2";

        var notes = ReportSection.ForMetrics("Notes", [new ReportMetric("Drafts", "2")]);
        notes.Id = "v4";

        var document = new ReportDocument
        {
            Title = "Posts dashboard",
            Containers =
            [
                new ReportContainer { Id = "k1", Title = "Author scorecard" },
                new ReportContainer { Id = "k2", Title = "Selection" },
            ],
        }
            .Add(authors)
            .Add(notes)
            .Add(posts)
            .Add(trend);

        // Act
        var content = new ExcelReportExportFormat().Serialize(document);

        // Assert
        using var stream = new MemoryStream(content);
        using var spreadsheetDocument = SpreadsheetDocument.Open(stream, false);
        var workbookPart = spreadsheetDocument.WorkbookPart;
        var sheets = workbookPart.Workbook.Sheets.Elements<Sheet>().ToArray();

        // A container is placed where its first section is, as on the page.
        Assert.Equal(["Author scorecard", "Notes", "Selection"], sheets.Select(sheet => sheet.Name.Value));

        var scorecard = GetSheetRows(workbookPart, sheets[0]).Select(GetCellValues).ToArray();
        Assert.Equal([["Authors"], ["Author"], ["Alice"], ["Bob"]], scorecard);

        // The sections of a container follow one another under their titles, each saying what filters it.
        var selection = GetSheetRows(workbookPart, sheets[2]).Select(GetCellValues).ToArray();
        Assert.Equal(
            [
                ["Posts"],
                ["Filtered by Authors: Alice"],
                [],
                ["Title"],
                ["Post 1"],
                [],
                ["Trend"],
                ["Filtered by Authors: Alice"],
                [],
                ["Label", "Posts"],
                ["Monday", "3"],
            ],
            selection);
    }

    [Fact]
    public void Serialize_WhenSectionNamesCollideOrContainInvalidCharacters_ShouldProduceValidUniqueSheetNames()
    {
        // Arrange
        var document = new ReportDocument()
            .Add(ReportSection.ForMetrics("Queue/Overview", [new ReportMetric("Calls", "8")]))
            .Add(ReportSection.ForMetrics("Queue:Overview", [new ReportMetric("Calls", "9")]));

        var exportFormat = new ExcelReportExportFormat();

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
        var exportFormat = new ExcelReportExportFormat();

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

    [Fact]
    public void Serialize_WhenTableCellsAreStyled_ShouldApplyFontColorFillAndBold()
    {
        // Arrange
        var document = new ReportDocument()
            .Add(ReportSection.ForTable(
                "Queues",
                [
                    new ReportColumn("Queue", ReportColumnAlign.Start, ReportStyle.Create("#FFFFFF", "#2563EB", bold: true)),
                    new ReportColumn("Count"),
                ],
                [
                    new ReportRow(["Support", "2"]).WithCellStyle(1, ReportStyle.Create("#FF0000")),
                ]));
        var exportFormat = new ExcelReportExportFormat();

        // Act
        var content = exportFormat.Serialize(document);

        // Assert
        using var stream = new MemoryStream(content);
        using var spreadsheetDocument = SpreadsheetDocument.Open(stream, false);
        var workbookPart = spreadsheetDocument.WorkbookPart;

        Assert.NotNull(workbookPart);
        Assert.NotNull(workbookPart.WorkbookStylesPart);

        var stylesheet = workbookPart.WorkbookStylesPart.Stylesheet;
        var sheet = Assert.Single(workbookPart.Workbook.Sheets.Elements<Sheet>());
        var rows = GetSheetRows(workbookPart, sheet);

        var headerCell = rows[0].Elements<Cell>().First();
        var headerFont = GetFont(stylesheet, headerCell);
        var headerFill = GetFill(stylesheet, headerCell);

        Assert.NotNull(headerFont.Bold);
        Assert.Equal("FFFFFFFF", headerFont.Color.Rgb.Value);
        Assert.Equal("FF2563EB", headerFill.PatternFill.ForegroundColor.Rgb.Value);

        var styledCell = rows[1].Elements<Cell>().ElementAt(1);
        var styledFont = GetFont(stylesheet, styledCell);

        Assert.Equal("FFFF0000", styledFont.Color.Rgb.Value);
    }

    [Fact]
    public void Serialize_WhenTableRowIsEmphasized_ShouldMakeCellsBold()
    {
        // Arrange
        var document = new ReportDocument()
            .Add(ReportSection.ForTable(
                "Queues",
                [new ReportColumn("Queue"), new ReportColumn("Count")],
                [new ReportRow(["All queues", "2"], ReportRowKind.GrandTotal)]));
        var exportFormat = new ExcelReportExportFormat();

        // Act
        var content = exportFormat.Serialize(document);

        // Assert
        using var stream = new MemoryStream(content);
        using var spreadsheetDocument = SpreadsheetDocument.Open(stream, false);
        var workbookPart = spreadsheetDocument.WorkbookPart;

        Assert.NotNull(workbookPart);

        var stylesheet = workbookPart.WorkbookStylesPart.Stylesheet;
        var sheet = Assert.Single(workbookPart.Workbook.Sheets.Elements<Sheet>());
        var rows = GetSheetRows(workbookPart, sheet);
        var totalCell = rows[1].Elements<Cell>().First();

        Assert.NotNull(GetFont(stylesheet, totalCell).Bold);
    }

    private static Font GetFont(Stylesheet stylesheet, Cell cell)
    {
        var cellFormat = stylesheet.CellFormats.Elements<CellFormat>().ElementAt((int)(cell.StyleIndex?.Value ?? 0));

        return stylesheet.Fonts.Elements<Font>().ElementAt((int)(cellFormat.FontId?.Value ?? 0));
    }

    private static Fill GetFill(Stylesheet stylesheet, Cell cell)
    {
        var cellFormat = stylesheet.CellFormats.Elements<CellFormat>().ElementAt((int)(cell.StyleIndex?.Value ?? 0));

        return stylesheet.Fills.Elements<Fill>().ElementAt((int)(cellFormat.FillId?.Value ?? 0));
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
