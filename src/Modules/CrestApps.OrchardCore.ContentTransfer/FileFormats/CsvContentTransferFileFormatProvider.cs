using System.Diagnostics;
using System.Text;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.ContentTransfer.Telemetry;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Tabular;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContentTransfer.FileFormats;

public sealed class CsvContentTransferFileFormatProvider : IContentTransferFileFormatProvider
{
    private readonly int _maxCellLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvContentTransferFileFormatProvider"/> class with the default
    /// limits.
    /// </summary>
    public CsvContentTransferFileFormatProvider()
        : this(Options.Create(new ContentImportOptions()))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvContentTransferFileFormatProvider"/> class.
    /// </summary>
    /// <param name="options">The import options; a cell longer than <see cref="ContentImportOptions.MaxCellLength"/>
    /// fails the file, so an unclosed quote can't read the whole upload into one cell.</param>
    public CsvContentTransferFileFormatProvider(IOptions<ContentImportOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _maxCellLength = options.Value.MaxCellLength;
    }

    public string FileExtension => ".csv";

    public string ContentType => "text/csv";

    public bool CanHandle(string fileName)
        => Path.GetExtension(fileName).Equals(FileExtension, StringComparison.OrdinalIgnoreCase);

    public IContentTransferFileReader CreateReader(Stream stream)
        => new CsvFileReader(stream, _maxCellLength);

    public IContentTransferFileWriter CreateWriter(Stream stream, string sheetName)
        => new CsvFileWriter(stream);

    private static void RecordOperation(string operation, long startTimestamp)
    {
        ContentTransferDiagnostics.Record(ContentTransferDiagnostics.FileOperationDuration, Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.FormatTag, "csv" },
            { CrestAppsOrchardCoreTelemetry.OperationTag, operation },
        });
    }

    /// <summary>
    /// Reads the rows of a CSV file as they are needed. The rows used to be read into memory when the reader was
    /// created, and every run of a long import read the whole file again, so a large file held hundreds of megabytes
    /// for the length of the run. A seekable stream is read twice instead: once to count the rows, once to return them.
    /// The records are read by the shared <see cref="CsvReader"/>, with a bound on the length of a cell.
    /// </summary>
    private sealed class CsvFileReader : IContentTransferFileReader
    {
        private readonly Stream _stream;
        private readonly long _startPosition;
        private readonly int _maxCellLength;
        private readonly List<string> _columnNames;

        private StreamReader _reader;
        private IEnumerator<string[]> _records;
        private List<string[]> _bufferedRows;
        private int? _rowCount;

        public CsvFileReader(Stream stream, int maxCellLength)
        {
            ArgumentNullException.ThrowIfNull(stream);

            _stream = stream;
            _maxCellLength = maxCellLength;
            _startPosition = stream.CanSeek ? stream.Position : 0;
            _reader = CreateStreamReader(stream);
            _records = CsvReader.ReadRecords(_reader, maxCellLength).GetEnumerator();

            if (!_records.MoveNext())
            {
                throw new InvalidOperationException("The CSV file is empty.");
            }

            _columnNames = [.. _records.Current];
        }

        public IReadOnlyList<string> GetColumnNames() => _columnNames;

        public int GetRowCount()
        {
            if (_rowCount is { } rowCount)
            {
                return rowCount;
            }

            // Counting reads the whole file before the first row is imported.
            var startTimestamp = Stopwatch.GetTimestamp();

            if (!_stream.CanSeek)
            {
                _rowCount = GetBufferedRows().Count;

                RecordOperation("count_rows", startTimestamp);

                return _rowCount.Value;
            }

            var count = 0;

            using (var reader = CreateStreamReader(Rewind()))
            {
                count = CsvReader.ReadRecords(reader, _maxCellLength).Skip(1).Count();
            }

            _rowCount = count;
            RecordOperation("count_rows", startTimestamp);

            return count;
        }

        public IEnumerable<string[]> ReadRows()
        {
            if (!_stream.CanSeek)
            {
                foreach (var bufferedRow in GetBufferedRows())
                {
                    yield return bufferedRow;
                }

                yield break;
            }

            using var reader = CreateStreamReader(Rewind());

            foreach (var record in CsvReader.ReadRecords(reader, _maxCellLength).Skip(1))
            {
                yield return ToRow(record);
            }
        }

        public void Dispose()
        {
            _records?.Dispose();
            _records = null;
            _reader?.Dispose();
            _reader = null;
        }

        private static StreamReader CreateStreamReader(Stream stream)
            => new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 64 * 1024, leaveOpen: true);

        private Stream Rewind()
        {
            _stream.Position = _startPosition;

            return _stream;
        }

        // A stream that cannot seek can be read only once, so its rows are kept in memory, as they always were.
        private List<string[]> GetBufferedRows()
        {
            if (_bufferedRows is null)
            {
                _bufferedRows = [];

                while (_records.MoveNext())
                {
                    _bufferedRows.Add(ToRow(_records.Current));
                }
            }

            return _bufferedRows;
        }

        private string[] ToRow(string[] values)
        {
            var row = new string[_columnNames.Count];

            // A blank line has no cells, so it reads as a row of no values, as it always did.
            if (values is [""])
            {
                return row;
            }

            for (var i = 0; i < Math.Min(values.Length, row.Length); i++)
            {
                row[i] = CsvReader.RemoveFormulaGuard(values[i]);
            }

            return row;
        }
    }

    private sealed class CsvFileWriter : IContentTransferFileWriter
    {
        private readonly StreamWriter _writer;

        public CsvFileWriter(Stream stream)
        {
            _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);
        }

        public void WriteHeader(IReadOnlyList<string> columnNames)
        {
            WriteLine(columnNames);
        }

        public void WriteRow(IReadOnlyList<string> values)
        {
            WriteLine(values);
        }

        public void Flush()
        {
            var startTimestamp = Stopwatch.GetTimestamp();

            _writer.Flush();
            RecordOperation("flush", startTimestamp);
        }

        public void Dispose()
        {
            _writer.Dispose();
        }

        private void WriteLine(IReadOnlyList<string> values)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    _writer.Write(',');
                }

                // Escaped with the formula guard: an exported file is opened in a spreadsheet, and a title such as
                // =HYPERLINK(...) must not run there. The reader takes the guard off again on import.
                _writer.Write(CsvWriter.Escape(values[i]));
            }

            _writer.WriteLine();
        }
    }
}
