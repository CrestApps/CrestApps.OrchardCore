using CrestApps.OrchardCore.ContentTransfer.OpenXml.Services;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CrestApps.OrchardCore.Tests.Modules.ContentTransfer;

public sealed class ExcelContentTransferFileFormatProviderTests
{
    [Fact]
    public void CanHandle_WithXlsxFile_ShouldReturnTrue()
    {
        var provider = new ExcelContentTransferFileFormatProvider();

        var result = provider.CanHandle("items.xlsx");

        Assert.True(result);
    }

    [Fact]
    public void CreateReaderAndWriter_ShouldRoundTripWorkbookContent()
    {
        var provider = new ExcelContentTransferFileFormatProvider();
        using var stream = new MemoryStream();

        using (var writer = provider.CreateWriter(stream, "Content Items"))
        {
            writer.WriteHeader(["Title", "Description"]);
            writer.WriteRow(["Item 1", "Value 1"]);
            writer.Flush();
        }

        stream.Position = 0;

        using var reader = provider.CreateReader(stream);
        var columns = reader.GetColumnNames();
        var rows = reader.ReadRows().ToList();

        Assert.Equal(["Title", "Description"], columns);
        Assert.Single(rows);
        Assert.Equal("Item 1", rows[0][0]);
        Assert.Equal("Value 1", rows[0][1]);
    }

    [Fact]
    public void CanHandle_IgnoresExtensionCase_AndRejectsOtherFormats()
    {
        var provider = new ExcelContentTransferFileFormatProvider();

        Assert.True(provider.CanHandle("items.XLSX"));
        Assert.False(provider.CanHandle("items.csv"));
    }

    [Fact]
    public void CreateReaderAndWriter_ShouldRoundTripSeveralRows_AndCountThem()
    {
        var provider = new ExcelContentTransferFileFormatProvider();
        using var stream = new MemoryStream();

        using (var writer = provider.CreateWriter(stream, "Leads"))
        {
            writer.WriteHeader(["Title", "Phone"]);
            writer.WriteRow(["Item 1", "+17025550101"]);
            writer.WriteRow(["Item 2", "+17025550102"]);
            writer.Flush();
        }

        stream.Position = 0;

        using var reader = provider.CreateReader(stream);
        var rows = reader.ReadRows().ToList();

        Assert.Equal(["Title", "Phone"], reader.GetColumnNames());
        Assert.Equal(2, reader.GetRowCount());
        Assert.Equal(2, rows.Count);
        Assert.Equal(["Item 1", "+17025550101"], rows[0]);
        Assert.Equal(["Item 2", "+17025550102"], rows[1]);
    }

    [Fact]
    public void CreateReader_ShouldResolveSharedStrings_IncludingRichTextRuns()
    {
        using var stream = CreateWorkbook(
            ["Name", "Notes"],
            [
                Row(1, SharedCell("A1", 0), SharedCell("B1", 1)),
                Row(2, SharedCell("A2", 2), SharedCell("B2", 3)),
            ]);

        using var reader = new ExcelContentTransferFileFormatProvider().CreateReader(stream);
        var rows = reader.ReadRows().ToList();

        Assert.Equal(["Name", "Notes"], reader.GetColumnNames());
        Assert.Single(rows);
        Assert.Equal("Jane Doe", rows[0][0]);
        Assert.Equal("Bold and plain", rows[0][1]);
    }

    [Fact]
    public void CreateReader_ShouldFillHeaderGaps_AndPlaceCellsByReference()
    {
        using var stream = CreateWorkbook(
            [],
            [
                // The header skips column B and leaves column D blank.
                Row(1, InlineCell("A1", "First"), InlineCell("C1", "Third"), InlineCell("D1", "  ")),
                Row(2, InlineCell("B2", "only b"), NumberCell("D2", "42")),
                Row(3, InlineCell("A3", "a"), InlineCell("C3", "c"), InlineCell("F3", "beyond the header")),
            ]);

        using var reader = new ExcelContentTransferFileFormatProvider().CreateReader(stream);
        var rows = reader.ReadRows().ToList();

        Assert.Equal(["First", "Col 2", "Third", "Col 4"], reader.GetColumnNames());
        Assert.Equal(2, reader.GetRowCount());
        Assert.Equal([null, "only b", null, "42"], rows[0]);
        Assert.Equal(["a", null, "c", null], rows[1]);
    }

    [Fact]
    public void CreateReader_ShouldCountAndStreamEveryRow_AndAllowReadingTwice()
    {
        const int dataRows = 5_000;
        var sheetRows = new List<Row>
        {
            Row(1, InlineCell("A1", "Id"), InlineCell("B1", "Phone")),
        };

        for (var i = 1; i <= dataRows; i++)
        {
            var rowIndex = (uint)(i + 1);
            sheetRows.Add(Row(rowIndex, NumberCell("A" + rowIndex, i.ToString()), InlineCell("B" + rowIndex, "+1702555" + i.ToString("D4"))));
        }

        using var stream = CreateWorkbook([], sheetRows);
        using var reader = new ExcelContentTransferFileFormatProvider().CreateReader(stream);

        Assert.Equal(dataRows, reader.GetRowCount());

        // A resumed import skips the rows it already processed; the reader must stream from the start each time.
        var firstPass = reader.ReadRows().Take(3).ToList();
        var secondPass = reader.ReadRows().ToList();

        Assert.Equal(["1", "+17025550001"], firstPass[0]);
        Assert.Equal(dataRows, secondPass.Count);
        Assert.Equal([dataRows.ToString(), "+1702555" + dataRows.ToString("D4")], secondPass[^1]);
    }

    [Fact]
    public void CreateReader_WithHeaderOnly_ShouldReturnNoRows()
    {
        using var stream = CreateWorkbook([], [Row(1, InlineCell("A1", "Title"))]);

        using var reader = new ExcelContentTransferFileFormatProvider().CreateReader(stream);

        Assert.Equal(["Title"], reader.GetColumnNames());
        Assert.Equal(0, reader.GetRowCount());
        Assert.Empty(reader.ReadRows());
    }

    [Fact]
    public void CreateReader_WithEmptySheet_ShouldThrow()
    {
        using var stream = CreateWorkbook([], []);

        var exception = Assert.Throws<InvalidOperationException>(() => new ExcelContentTransferFileFormatProvider().CreateReader(stream));

        Assert.Equal("Unable to find a tab in the file that contains data.", exception.Message);
    }

    private static MemoryStream CreateWorkbook(string[] headerSharedStrings, IEnumerable<Row> rows)
    {
        var stream = new MemoryStream();

        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            if (headerSharedStrings.Length > 0)
            {
                var sharedStringPart = workbookPart.AddNewPart<SharedStringTablePart>();
                var table = new SharedStringTable();

                foreach (var text in headerSharedStrings)
                {
                    table.Append(new SharedStringItem(new Text(text)));
                }

                table.Append(new SharedStringItem(new Text("Jane Doe")));
                table.Append(new SharedStringItem(
                    new Run(new RunProperties(new Bold()), new Text("Bold")),
                    new Run(new Text(" and plain") { Space = SpaceProcessingModeValues.Preserve })));

                sharedStringPart.SharedStringTable = table;
            }

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData(rows));

            workbookPart.Workbook.AppendChild(new Sheets()).Append(new Sheet()
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1",
            });

            workbookPart.Workbook.Save();
        }

        stream.Position = 0;

        return stream;
    }

    private static Row Row(uint rowIndex, params Cell[] cells)
        => new(cells) { RowIndex = rowIndex };

    private static Cell SharedCell(string reference, int index)
        => new()
        {
            CellReference = reference,
            DataType = CellValues.SharedString,
            CellValue = new CellValue(index.ToString()),
        };

    private static Cell InlineCell(string reference, string value)
        => new()
        {
            CellReference = reference,
            DataType = CellValues.String,
            CellValue = new CellValue(value),
        };

    private static Cell NumberCell(string reference, string value)
        => new()
        {
            CellReference = reference,
            CellValue = new CellValue(value),
        };
}
