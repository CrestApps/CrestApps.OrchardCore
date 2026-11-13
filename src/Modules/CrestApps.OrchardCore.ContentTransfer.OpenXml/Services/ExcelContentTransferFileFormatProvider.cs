using System.Diagnostics;
using CrestApps.OrchardCore.ContentTransfer.Telemetry;
using CrestApps.OrchardCore.Diagnostics;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using CrestApps.OrchardCore.ContentTransfer.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContentTransfer.OpenXml.Services;

public sealed class ExcelContentTransferFileFormatProvider : IContentTransferFileFormatProvider
{
    private readonly ContentImportOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExcelContentTransferFileFormatProvider"/> class with the default
    /// limits.
    /// </summary>
    public ExcelContentTransferFileFormatProvider()
        : this(Options.Create(new ContentImportOptions()))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExcelContentTransferFileFormatProvider"/> class.
    /// </summary>
    /// <param name="options">The import options, which hold the limits of a file.</param>
    public ExcelContentTransferFileFormatProvider(IOptions<ContentImportOptions> options)
    {
        _options = options.Value;
    }

    public string FileExtension => ".xlsx";

    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public bool CanHandle(string fileName)
        => Path.GetExtension(fileName).Equals(FileExtension, StringComparison.OrdinalIgnoreCase);

    public IContentTransferFileReader CreateReader(Stream stream)
        => new ExcelFileReader(stream, _options);

    public IContentTransferFileWriter CreateWriter(Stream stream, string sheetName)
        => new ExcelFileWriter(stream, sheetName);

    private static void RecordOperation(string operation, long startTimestamp)
    {
        ContentTransferDiagnostics.Record(ContentTransferDiagnostics.FileOperationDuration, Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.FormatTag, "xlsx" },
            { CrestAppsOrchardCoreTelemetry.OperationTag, operation },
        });
    }

    /// <summary>
    /// Reads the first worksheet with <see cref="OpenXmlReader"/> so only one row is held in memory at a time. The row
    /// count is taken in one pass when the reader opens and every <see cref="ReadRows"/> call streams the sheet again.
    /// </summary>
    private sealed class ExcelFileReader : IContentTransferFileReader
    {
        private const string NoDataMessage = "Unable to find a tab in the file that contains data.";

        private readonly SpreadsheetDocument _document;
        private readonly WorksheetPart _worksheetPart;
        private readonly List<string> _sharedStrings;
        private readonly List<string> _columnNames;
        private readonly int _rowCount;
        private readonly int _maxCellLength;

        public ExcelFileReader(Stream stream, ContentImportOptions options)
        {
            _maxCellLength = options.MaxCellLength;

            // Opening reads the shared strings and counts the rows of the sheet before the first row is imported.
            var startTimestamp = Stopwatch.GetTimestamp();

            _document = SpreadsheetDocument.Open(stream, false);
            var workbookPart = _document.WorkbookPart
                ?? throw new InvalidOperationException("Unable to read the uploaded file.");

            var firstSheet = workbookPart.Workbook.Descendants<Sheet>().FirstOrDefault()
                ?? throw new InvalidOperationException(NoDataMessage);

            _worksheetPart = (WorksheetPart)workbookPart.GetPartById(firstSheet.Id);
            _sharedStrings = ReadSharedStrings(workbookPart.GetPartsOfType<SharedStringTablePart>().FirstOrDefault(), options.MaxSharedStrings, _maxCellLength);

            Row headerRow;
            using (var reader = OpenXmlReader.Create(_worksheetPart))
            {
                headerRow = ReadRowElements(reader).FirstOrDefault()
                    ?? throw new InvalidOperationException(NoDataMessage);
            }

            var columnNames = new List<string>();
            foreach (var cell in headerRow.Descendants<Cell>())
            {
                var columnName = GetCellValue(cell)?.Trim() ?? string.Empty;
                var columnIndex = GetColumnIndexFromCellReference(cell.CellReference);

                // A header cell that names a far column would otherwise add a name for every column before it.
                if (columnIndex < 0 || columnIndex >= options.MaxColumns)
                {
                    throw new InvalidOperationException($"The file has more than {options.MaxColumns} columns, the most an import may have.");
                }

                while (columnNames.Count < columnIndex)
                {
                    columnNames.Add("Col " + (columnNames.Count + 1));
                }

                if (string.IsNullOrEmpty(columnName))
                {
                    columnName = "Col " + (columnIndex + 1);
                }

                columnNames.Add(columnName);
            }

            _columnNames = columnNames;
            _rowCount = Math.Max(CountRows(_worksheetPart) - 1, 0);

            if (_rowCount > options.MaxRows)
            {
                throw new InvalidOperationException($"The file has more than {options.MaxRows} rows, the most an import may have.");
            }
            RecordOperation("open", startTimestamp);
        }

        public IReadOnlyList<string> GetColumnNames() => _columnNames;

        public int GetRowCount() => _rowCount;

        public IEnumerable<string[]> ReadRows()
        {
            var columnCount = _columnNames.Count;

            using var reader = OpenXmlReader.Create(_worksheetPart);

            foreach (var sheetRow in ReadRowElements(reader).Skip(1))
            {
                var values = new string[columnCount];

                foreach (var cell in sheetRow.Descendants<Cell>())
                {
                    var colIndex = GetColumnIndexFromCellReference(cell.CellReference);

                    if (colIndex >= 0 && colIndex < columnCount)
                    {
                        values[colIndex] = GetCellValue(cell);
                    }
                }

                yield return values;
            }
        }

        public void Dispose()
        {
            _document.Dispose();
        }

        private static List<string> ReadSharedStrings(SharedStringTablePart sharedStringTablePart, int maxSharedStrings, int maxCellLength)
        {
            if (sharedStringTablePart == null)
            {
                return null;
            }

            var sharedStrings = new List<string>();

            using var reader = OpenXmlReader.Create(sharedStringTablePart);

            while (reader.Read())
            {
                if (reader.ElementType == typeof(SharedStringItem) && reader.IsStartElement)
                {
                    if (sharedStrings.Count >= maxSharedStrings)
                    {
                        throw new InvalidOperationException($"The file has more than {maxSharedStrings} shared strings, the most an import may have.");
                    }

                    // Loading one item keeps the text identical to the item's InnerText, rich-text runs included. All of
                    // them are held while the file is read, so each is held to the cell limit as well.
                    var text = reader.LoadCurrentElement().InnerText;

                    if (text.Length > maxCellLength)
                    {
                        throw new InvalidOperationException($"A cell of the file holds more than {maxCellLength} characters, the most an import may have.");
                    }

                    sharedStrings.Add(text);
                }
            }

            return sharedStrings;
        }

        /// <summary>
        /// Loads the direct child rows of the sheet data one at a time, matching <c>SheetData.Elements&lt;Row&gt;()</c>.
        /// </summary>
        private static IEnumerable<Row> ReadRowElements(OpenXmlReader reader)
        {
            if (!MoveToSheetDataFirstChild(reader))
            {
                yield break;
            }

            while (!reader.EOF)
            {
                if (reader.ElementType == typeof(SheetData) && reader.IsEndElement)
                {
                    yield break;
                }

                if (reader.ElementType == typeof(Row) && reader.IsStartElement)
                {
                    // Loading the row leaves the reader on the node that follows it, so the loop must not step again.
                    yield return (Row)reader.LoadCurrentElement();

                    continue;
                }

                if (!reader.ReadNextSibling())
                {
                    yield break;
                }
            }
        }

        private static int CountRows(WorksheetPart worksheetPart)
        {
            using var reader = OpenXmlReader.Create(worksheetPart);

            if (!MoveToSheetDataFirstChild(reader))
            {
                return 0;
            }

            var count = 0;

            do
            {
                if (reader.ElementType == typeof(Row) && reader.IsStartElement)
                {
                    count++;
                }
            }
            while (reader.ReadNextSibling());

            return count;
        }

        private static bool MoveToSheetDataFirstChild(OpenXmlReader reader)
        {
            while (reader.Read())
            {
                if (reader.ElementType == typeof(SheetData) && reader.IsStartElement)
                {
                    return reader.ReadFirstChild();
                }
            }

            return false;
        }

        private string GetCellValue(Cell cell)
        {
            var value = GetRawCellValue(cell);

            if (value is not null && value.Length > _maxCellLength)
            {
                throw new InvalidOperationException($"A cell of the file holds more than {_maxCellLength} characters, the most an import may have.");
            }

            return value;
        }

        private string GetRawCellValue(Cell cell)
        {
            if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString
                && _sharedStrings != null)
            {
                if (int.TryParse(cell.InnerText, out var index) && index >= 0 && index < _sharedStrings.Count)
                {
                    return _sharedStrings[index];
                }
            }

            return cell.CellValue?.Text ?? cell.InnerText;
        }

        private static int GetColumnIndexFromCellReference(string cellReference)
        {
            long columnIndex = 0;

            foreach (var ch in cellReference ?? string.Empty)
            {
                if (char.IsLetter(ch))
                {
                    columnIndex = (columnIndex * 26) + (char.ToUpperInvariant(ch) - 'A' + 1);

                    // A reference past any real column must not wrap around to a small or negative index.
                    if (columnIndex > int.MaxValue)
                    {
                        return int.MaxValue;
                    }
                }
                else
                {
                    break;
                }
            }

            return (int)columnIndex - 1;
        }
    }

    private sealed class ExcelFileWriter : IContentTransferFileWriter
    {
        private readonly SpreadsheetDocument _document;
        private readonly SheetData _sheetData;
        private readonly WorkbookPart _workbookPart;
        private uint _currentRowIndex = 1;
        private int _columnCount;

        public ExcelFileWriter(Stream stream, string sheetName)
        {
            _document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook);
            _workbookPart = _document.AddWorkbookPart();
            _workbookPart.Workbook = new Workbook();

            var worksheetPart = _workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData());

            var sheets = _workbookPart.Workbook.AppendChild(new Sheets());
            var truncatedName = sheetName?.Length > 31 ? sheetName[..31] : sheetName;
            var sheet = new Sheet()
            {
                Id = _workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = truncatedName,
            };
            sheets.Append(sheet);

            _sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
        }

        public void WriteHeader(IReadOnlyList<string> columnNames)
        {
            _columnCount = columnNames.Count;
            var headerRow = new Row() { RowIndex = _currentRowIndex };
            _sheetData.Append(headerRow);

            for (var i = 0; i < columnNames.Count; i++)
            {
                var cell = new Cell()
                {
                    CellReference = GetCellReference((uint)(i + 1), _currentRowIndex),
                    DataType = CellValues.String,
                    CellValue = new CellValue(columnNames[i]),
                };
                headerRow.Append(cell);
            }

            _currentRowIndex++;
        }

        public void WriteRow(IReadOnlyList<string> values)
        {
            var row = new Row() { RowIndex = _currentRowIndex };
            _sheetData.Append(row);

            for (var i = 0; i < values.Count && i < _columnCount; i++)
            {
                var cell = new Cell()
                {
                    CellReference = GetCellReference((uint)(i + 1), _currentRowIndex),
                    DataType = CellValues.String,
                    CellValue = new CellValue(values[i] ?? string.Empty),
                };
                row.Append(cell);
            }

            _currentRowIndex++;
        }

        public void Flush()
        {
            var startTimestamp = Stopwatch.GetTimestamp();

            _workbookPart.Workbook.Save();
            RecordOperation("flush", startTimestamp);
        }

        public void Dispose()
        {
            // The workbook is written to the stream when the document closes.
            var startTimestamp = Stopwatch.GetTimestamp();

            _document.Dispose();
            RecordOperation("close", startTimestamp);
        }

        private static string GetCellReference(uint columnIndex, uint rowIndex)
        {
            var columnName = string.Empty;
            var dividend = columnIndex;

            while (dividend > 0)
            {
                var modulo = (dividend - 1) % 26;
                columnName = Convert.ToChar(65 + modulo) + columnName;
                dividend = (dividend - modulo) / 26;
            }

            return columnName + rowIndex;
        }
    }
}
