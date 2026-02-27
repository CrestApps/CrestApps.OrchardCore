using CrestApps.OrchardCore.AI.Documents.OpenXml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Drawing = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using WpParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using WpRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using WpText = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace CrestApps.OrchardCore.Tests.Helpers.DocumentReaders;

public sealed class OpenXmlIngestionDocumentReaderTests
{
    private readonly OpenXmlIngestionDocumentReader _reader = new();

    #region Word (.docx)

    [Fact]
    public async Task ReadAsync_WordDocument_ExtractsParagraphs()
    {
        using var stream = CreateWordDocument("Hello World", "Second paragraph");

        var result = await _reader.ReadAsync(
            stream,
            "test.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            TestContext.Current.CancellationToken);

        Assert.Single(result.Sections);
        Assert.Equal(2, result.Sections[0].Elements.Count);
        Assert.Equal("Hello World", result.Sections[0].Elements[0].Text);
        Assert.Equal("Second paragraph", result.Sections[0].Elements[1].Text);
    }

    [Fact]
    public async Task ReadAsync_WordDocument_SkipsEmptyParagraphs()
    {
        using var stream = CreateWordDocument("Content", "", "More content");

        var result = await _reader.ReadAsync(
            stream,
            "test.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            TestContext.Current.CancellationToken);

        Assert.Single(result.Sections);
        Assert.Equal(2, result.Sections[0].Elements.Count);
    }

    [Fact]
    public async Task ReadAsync_EmptyWordDocument_ReturnsNoSections()
    {
        using var stream = CreateWordDocument();

        var result = await _reader.ReadAsync(
            stream,
            "test.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            TestContext.Current.CancellationToken);

        Assert.Empty(result.Sections);
    }

    #endregion

    #region Excel (.xlsx) - Shared Strings

    [Fact]
    public async Task ReadAsync_ExcelWithSharedStrings_ExtractsRows()
    {
        using var stream = CreateExcelWithSharedStrings(
        [
            ["Title", "Question", "Answer"],
            ["Thor Weapon", "What is Thor's weapon?", "Mjolnir"],
        ]);

        var result = await _reader.ReadAsync(
            stream,
            "test.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            TestContext.Current.CancellationToken);

        Assert.Single(result.Sections);
        Assert.Equal(2, result.Sections[0].Elements.Count);
        Assert.Equal("Title\tQuestion\tAnswer", result.Sections[0].Elements[0].Text);
        Assert.Equal("Thor Weapon\tWhat is Thor's weapon?\tMjolnir", result.Sections[0].Elements[1].Text);
    }

    [Fact]
    public async Task ReadAsync_ExcelWithInlineStrings_ExtractsRows()
    {
        using var stream = CreateExcelWithInlineStrings(
        [
            ["Name", "Value"],
            ["Key1", "Data1"],
        ]);

        var result = await _reader.ReadAsync(
            stream,
            "test.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            TestContext.Current.CancellationToken);

        Assert.Single(result.Sections);
        Assert.Equal(2, result.Sections[0].Elements.Count);
        Assert.Equal("Name\tValue", result.Sections[0].Elements[0].Text);
        Assert.Equal("Key1\tData1", result.Sections[0].Elements[1].Text);
    }

    [Fact]
    public async Task ReadAsync_ExcelWithNumericValues_ExtractsRows()
    {
        using var stream = CreateExcelWithNumericValues(
        [
            [1.0, 2.5, 3.7],
            [10.0, 20.0, 30.0],
        ]);

        var result = await _reader.ReadAsync(
            stream,
            "test.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            TestContext.Current.CancellationToken);

        Assert.Single(result.Sections);
        Assert.Equal(2, result.Sections[0].Elements.Count);
        Assert.Equal("1\t2.5\t3.7", result.Sections[0].Elements[0].Text);
        Assert.Equal("10\t20\t30", result.Sections[0].Elements[1].Text);
    }

    [Fact]
    public async Task ReadAsync_ExcelWithBooleanValues_ExtractsRows()
    {
        using var stream = CreateExcelWithBooleans(true, false);

        var result = await _reader.ReadAsync(
            stream,
            "test.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            TestContext.Current.CancellationToken);

        Assert.Single(result.Sections);
        Assert.Single(result.Sections[0].Elements);
        Assert.Equal("TRUE\tFALSE", result.Sections[0].Elements[0].Text);
    }

    [Fact]
    public async Task ReadAsync_ExcelWithMultipleSheets_ExtractsAll()
    {
        using var stream = CreateExcelWithMultipleSheets();

        var result = await _reader.ReadAsync(
            stream,
            "test.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            TestContext.Current.CancellationToken);

        Assert.Single(result.Sections);

        // Should have rows from both sheets.
        Assert.True(result.Sections[0].Elements.Count >= 2);
    }

    [Fact]
    public async Task ReadAsync_EmptyExcel_ReturnsNoSections()
    {
        using var stream = CreateEmptyExcel();

        var result = await _reader.ReadAsync(
            stream,
            "test.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            TestContext.Current.CancellationToken);

        Assert.Empty(result.Sections);
    }

    [Fact]
    public async Task ReadAsync_ExcelWithMixedCellTypes_ExtractsAll()
    {
        using var stream = CreateExcelWithMixedTypes();

        var result = await _reader.ReadAsync(
            stream,
            "test.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            TestContext.Current.CancellationToken);

        Assert.Single(result.Sections);
        Assert.True(result.Sections[0].Elements.Count > 0);
    }

    #endregion

    #region PowerPoint (.pptx)

    [Fact]
    public async Task ReadAsync_PowerPoint_ExtractsSlideText()
    {
        using var stream = CreatePowerPoint("Slide 1 Title", "Slide 2 Content");

        var result = await _reader.ReadAsync(
            stream,
            "test.pptx",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            TestContext.Current.CancellationToken);

        Assert.Single(result.Sections);
        Assert.Equal(2, result.Sections[0].Elements.Count);
    }

    [Fact]
    public async Task ReadAsync_EmptyPowerPoint_ReturnsNoSections()
    {
        using var stream = CreateEmptyPowerPoint();

        var result = await _reader.ReadAsync(
            stream,
            "test.pptx",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            TestContext.Current.CancellationToken);

        Assert.Empty(result.Sections);
    }

    #endregion

    #region Unsupported Media Types

    [Fact]
    public async Task ReadAsync_UnsupportedMediaType_ThrowsNotSupportedException()
    {
        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _reader.ReadAsync(stream, "test.txt", "text/plain", TestContext.Current.CancellationToken));
    }

    #endregion

    #region Non-Seekable Stream

    [Fact]
    public async Task ReadAsync_NonSeekableStream_ExtractsCorrectly()
    {
        using var seekableStream = CreateWordDocument("Test content");
        using var nonSeekable = new NonSeekableStream(seekableStream);

        var result = await _reader.ReadAsync(
            nonSeekable,
            "test.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            TestContext.Current.CancellationToken);

        Assert.Single(result.Sections);
        Assert.Single(result.Sections[0].Elements);
        Assert.Equal("Test content", result.Sections[0].Elements[0].Text);
    }

    #endregion

    #region Helpers

    private static MemoryStream CreateWordDocument(params string[] paragraphs)
    {
        var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            var body = new Body();

            foreach (var text in paragraphs)
            {
                body.AppendChild(new WpParagraph(new WpRun(new WpText(text))));
            }

            mainPart.Document = new Document(body);
        }

        stream.Position = 0;

        return stream;
    }

    private static MemoryStream CreateExcelWithSharedStrings(string[][] rows)
    {
        var stream = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = doc.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            // Build shared string table.
            var allStrings = rows.SelectMany(r => r).Distinct().ToList();
            var sstPart = workbookPart.AddNewPart<SharedStringTablePart>();
            var sst = new SharedStringTable();

            foreach (var s in allStrings)
            {
                sst.AppendChild(new SharedStringItem(new DocumentFormat.OpenXml.Spreadsheet.Text(s)));
            }

            sstPart.SharedStringTable = sst;

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();

            uint rowIndex = 1;

            foreach (var rowData in rows)
            {
                var row = new Row { RowIndex = rowIndex };
                var colIndex = 0;

                foreach (var cellValue in rowData)
                {
                    var cellRef = $"{(char)('A' + colIndex)}{rowIndex}";
                    var cell = new Cell
                    {
                        CellReference = cellRef,
                        DataType = CellValues.SharedString,
                        CellValue = new CellValue(allStrings.IndexOf(cellValue).ToString()),
                    };

                    row.AppendChild(cell);
                    colIndex++;
                }

                sheetData.AppendChild(row);
                rowIndex++;
            }

            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.AppendChild(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1",
            });
        }

        stream.Position = 0;

        return stream;
    }

    private static MemoryStream CreateExcelWithInlineStrings(string[][] rows)
    {
        var stream = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = doc.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();

            uint rowIndex = 1;

            foreach (var rowData in rows)
            {
                var row = new Row { RowIndex = rowIndex };
                var colIndex = 0;

                foreach (var cellValue in rowData)
                {
                    var cellRef = $"{(char)('A' + colIndex)}{rowIndex}";
                    var cell = new Cell
                    {
                        CellReference = cellRef,
                        DataType = CellValues.InlineString,
                        InlineString = new InlineString(new DocumentFormat.OpenXml.Spreadsheet.Text(cellValue)),
                    };

                    row.AppendChild(cell);
                    colIndex++;
                }

                sheetData.AppendChild(row);
                rowIndex++;
            }

            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.AppendChild(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1",
            });
        }

        stream.Position = 0;

        return stream;
    }

    private static MemoryStream CreateExcelWithNumericValues(double[][] rows)
    {
        var stream = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = doc.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();

            uint rowIndex = 1;

            foreach (var rowData in rows)
            {
                var row = new Row { RowIndex = rowIndex };
                var colIndex = 0;

                foreach (var value in rowData)
                {
                    var cellRef = $"{(char)('A' + colIndex)}{rowIndex}";
                    var cell = new Cell
                    {
                        CellReference = cellRef,
                        DataType = CellValues.Number,
                        CellValue = new CellValue(value.ToString()),
                    };

                    row.AppendChild(cell);
                    colIndex++;
                }

                sheetData.AppendChild(row);
                rowIndex++;
            }

            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.AppendChild(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1",
            });
        }

        stream.Position = 0;

        return stream;
    }

    private static MemoryStream CreateExcelWithBooleans(params bool[] values)
    {
        var stream = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = doc.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            var row = new Row { RowIndex = 1 };

            for (var i = 0; i < values.Length; i++)
            {
                var cellRef = $"{(char)('A' + i)}1";
                row.AppendChild(new Cell
                {
                    CellReference = cellRef,
                    DataType = CellValues.Boolean,
                    CellValue = new CellValue(values[i] ? "1" : "0"),
                });
            }

            sheetData.AppendChild(row);
            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.AppendChild(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1",
            });
        }

        stream.Position = 0;

        return stream;
    }

    private static MemoryStream CreateExcelWithMultipleSheets()
    {
        var stream = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = doc.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var sheets = workbookPart.Workbook.AppendChild(new Sheets());

            for (uint sheetNum = 1; sheetNum <= 2; sheetNum++)
            {
                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var sheetData = new SheetData();
                var row = new Row { RowIndex = 1 };

                row.AppendChild(new Cell
                {
                    CellReference = "A1",
                    DataType = CellValues.InlineString,
                    InlineString = new InlineString(new DocumentFormat.OpenXml.Spreadsheet.Text($"Sheet{sheetNum} Data")),
                });

                sheetData.AppendChild(row);
                worksheetPart.Worksheet = new Worksheet(sheetData);

                sheets.AppendChild(new Sheet
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = sheetNum,
                    Name = $"Sheet{sheetNum}",
                });
            }
        }

        stream.Position = 0;

        return stream;
    }

    private static MemoryStream CreateExcelWithMixedTypes()
    {
        var stream = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = doc.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var sstPart = workbookPart.AddNewPart<SharedStringTablePart>();
            var sst = new SharedStringTable();
            sst.AppendChild(new SharedStringItem(new DocumentFormat.OpenXml.Spreadsheet.Text("SharedText")));
            sstPart.SharedStringTable = sst;

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            var row = new Row { RowIndex = 1 };

            // Shared string cell.
            row.AppendChild(new Cell
            {
                CellReference = "A1",
                DataType = CellValues.SharedString,
                CellValue = new CellValue("0"),
            });

            // Inline string cell.
            row.AppendChild(new Cell
            {
                CellReference = "B1",
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new DocumentFormat.OpenXml.Spreadsheet.Text("InlineText")),
            });

            // Numeric cell (no DataType = default numeric).
            row.AppendChild(new Cell
            {
                CellReference = "C1",
                CellValue = new CellValue("42"),
            });

            // Boolean cell.
            row.AppendChild(new Cell
            {
                CellReference = "D1",
                DataType = CellValues.Boolean,
                CellValue = new CellValue("1"),
            });

            sheetData.AppendChild(row);
            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.AppendChild(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1",
            });
        }

        stream.Position = 0;

        return stream;
    }

    private static MemoryStream CreateEmptyExcel()
    {
        var stream = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = doc.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData());

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.AppendChild(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1",
            });
        }

        stream.Position = 0;

        return stream;
    }

    private static MemoryStream CreatePowerPoint(params string[] slideTexts)
    {
        var stream = new MemoryStream();
        using (var doc = PresentationDocument.Create(stream, PresentationDocumentType.Presentation))
        {
            var presentationPart = doc.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation();
            var slideIdList = presentationPart.Presentation.AppendChild(new P.SlideIdList());

            uint slideId = 256;

            foreach (var text in slideTexts)
            {
                var slidePart = presentationPart.AddNewPart<SlidePart>();
                slidePart.Slide = new P.Slide(
                    new P.CommonSlideData(
                        new P.ShapeTree(
                            new P.NonVisualGroupShapeProperties(
                                new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                                new P.NonVisualGroupShapeDrawingProperties(),
                                new P.ApplicationNonVisualDrawingProperties()),
                            new P.GroupShapeProperties(new Drawing.TransformGroup()),
                            new P.Shape(
                                new P.NonVisualShapeProperties(
                                    new P.NonVisualDrawingProperties { Id = 2, Name = "TextBox" },
                                    new P.NonVisualShapeDrawingProperties(),
                                    new P.ApplicationNonVisualDrawingProperties()),
                                new P.ShapeProperties(),
                                new P.TextBody(
                                    new Drawing.BodyProperties(),
                                    new Drawing.ListStyle(),
                                    new Drawing.Paragraph(
                                        new Drawing.Run(
                                            new Drawing.RunProperties { Language = "en-US" },
                                            new Drawing.Text(text))))))));

                slideIdList.AppendChild(new P.SlideId
                {
                    Id = slideId++,
                    RelationshipId = presentationPart.GetIdOfPart(slidePart),
                });
            }
        }

        stream.Position = 0;

        return stream;
    }

    private static MemoryStream CreateEmptyPowerPoint()
    {
        var stream = new MemoryStream();
        using (var doc = PresentationDocument.Create(stream, PresentationDocumentType.Presentation))
        {
            var presentationPart = doc.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation();
            presentationPart.Presentation.AppendChild(new P.SlideIdList());
        }

        stream.Position = 0;

        return stream;
    }

    /// <summary>
    /// A wrapper stream that disables seeking to test the non-seekable code path.
    /// </summary>
    private sealed class NonSeekableStream : Stream
    {
        private readonly Stream _inner;

        public NonSeekableStream(Stream inner) => _inner = inner;

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            => _inner.CopyToAsync(destination, bufferSize, cancellationToken);
    }

    #endregion
}
