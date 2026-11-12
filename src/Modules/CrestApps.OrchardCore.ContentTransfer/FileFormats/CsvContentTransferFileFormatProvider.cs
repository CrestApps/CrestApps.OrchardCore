using System.Diagnostics;
using System.Text;
using CrestApps.OrchardCore.ContentTransfer.Telemetry;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Tabular;

namespace CrestApps.OrchardCore.ContentTransfer.FileFormats;

public sealed class CsvContentTransferFileFormatProvider : IContentTransferFileFormatProvider
{
    public string FileExtension => ".csv";

    public string ContentType => "text/csv";

    public bool CanHandle(string fileName)
        => Path.GetExtension(fileName).Equals(FileExtension, StringComparison.OrdinalIgnoreCase);

    public IContentTransferFileReader CreateReader(Stream stream)
        => new CsvFileReader(stream);

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
    /// Reads the rows of a CSV file as they are needed, through the shared <see cref="CsvReader"/>. The rows used to be
    /// read into memory when the reader was created, and every run of a long import read the whole file again, so a
    /// large file held hundreds of megabytes for the length of the run. A seekable stream is read twice instead: once
    /// to count the rows, once to return them.
    /// </summary>
    private sealed class CsvFileReader : IContentTransferFileReader
    {
        private readonly Stream _stream;
        private readonly long _startPosition;
        private readonly List<string> _columnNames;

        private StreamReader _reader;
        private IEnumerator<string[]> _records;
        private List<string[]> _bufferedRows;
        private int? _rowCount;

        public CsvFileReader(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            _stream = stream;
            _startPosition = stream.CanSeek ? stream.Position : 0;
            _reader = CreateStreamReader(stream);
            _records = CsvReader.ReadRecords(_reader).GetEnumerator();

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

            foreach (var _ in ReadRecordsAfterHeader())
            {
                count++;
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

            foreach (var record in ReadRecordsAfterHeader())
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

        private IEnumerable<string[]> ReadRecordsAfterHeader()
        {
            _stream.Position = _startPosition;

            using var reader = CreateStreamReader(_stream);

            foreach (var record in CsvReader.ReadRecords(reader).Skip(1))
            {
                yield return record;
            }
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

            // A blank line is a row with no values, not one empty first value.
            if (values.Length == 1 && values[0].Length == 0)
            {
                return row;
            }

            for (var i = 0; i < Math.Min(values.Length, row.Length); i++)
            {
                row[i] = values[i];
            }

            return row;
        }
    }

    /// <summary>
    /// Writes CSV through the shared <see cref="CsvWriter"/>, which quotes values as RFC 4180 asks and guards a value
    /// that starts like a spreadsheet formula, so an exported file never runs one when it is opened.
    /// </summary>
    private sealed class CsvFileWriter : IContentTransferFileWriter
    {
        private readonly StreamWriter _writer;

        public CsvFileWriter(Stream stream)
        {
            _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);
        }

        public void WriteHeader(IReadOnlyList<string> columnNames)
        {
            CsvWriter.WriteRow(_writer, columnNames);
        }

        public void WriteRow(IReadOnlyList<string> values)
        {
            CsvWriter.WriteRow(_writer, values);
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
    }
}
