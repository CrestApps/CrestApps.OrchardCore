using System.Text;

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

    /// <summary>
    /// Reads the rows of a CSV file as they are needed. The rows used to be read into memory when the reader was
    /// created, and every run of a long import read the whole file again, so a large file held hundreds of megabytes
    /// for the length of the run. A seekable stream is read twice instead: once to count the rows, once to return them.
    /// </summary>
    private sealed class CsvFileReader : IContentTransferFileReader
    {
        private readonly Stream _stream;
        private readonly long _startPosition;
        private readonly List<string> _columnNames;

        private StreamReader _reader;
        private List<string[]> _bufferedRows;
        private int? _rowCount;

        public CsvFileReader(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            _stream = stream;
            _startPosition = stream.CanSeek ? stream.Position : 0;
            _reader = CreateStreamReader(stream);

            var headerLine = ReadLogicalLine(_reader)
                ?? throw new InvalidOperationException("The CSV file is empty.");

            _columnNames = ParseLine(headerLine);
        }

        public IReadOnlyList<string> GetColumnNames() => _columnNames;

        public int GetRowCount()
        {
            if (_rowCount is { } rowCount)
            {
                return rowCount;
            }

            if (!_stream.CanSeek)
            {
                _rowCount = GetBufferedRows().Count;

                return _rowCount.Value;
            }

            var count = 0;

            using (var reader = OpenAfterHeader())
            {
                while (ReadLogicalLine(reader) != null)
                {
                    count++;
                }
            }

            _rowCount = count;

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

            using var reader = OpenAfterHeader();
            string line;

            while ((line = ReadLogicalLine(reader)) != null)
            {
                yield return ToRow(line);
            }
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _reader = null;
        }

        private static StreamReader CreateStreamReader(Stream stream)
            => new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 64 * 1024, leaveOpen: true);

        private StreamReader OpenAfterHeader()
        {
            _stream.Position = _startPosition;

            var reader = CreateStreamReader(_stream);
            ReadLogicalLine(reader);

            return reader;
        }

        // A stream that cannot seek can be read only once, so its rows are kept in memory, as they always were.
        private List<string[]> GetBufferedRows()
        {
            if (_bufferedRows is null)
            {
                _bufferedRows = [];
                string line;

                while ((line = ReadLogicalLine(_reader)) != null)
                {
                    _bufferedRows.Add(ToRow(line));
                }
            }

            return _bufferedRows;
        }

        private string[] ToRow(string line)
        {
            var values = ParseLine(line);
            var row = new string[_columnNames.Count];

            for (var i = 0; i < Math.Min(values.Count, row.Length); i++)
            {
                row[i] = values[i];
            }

            return row;
        }

        /// <summary>
        /// Reads a logical CSV line that may span multiple physical lines due to quoted fields.
        /// </summary>
        private static string ReadLogicalLine(StreamReader reader)
        {
            var firstLine = reader.ReadLine();
            if (firstLine == null)
            {
                return null;
            }

            // Fast path: if the line has an even number of quotes, it's a complete logical line.
            if (CountQuotes(firstLine) % 2 == 0)
            {
                return firstLine;
            }

            // Slow path: the line has an unclosed quote, so we need to read more lines.
            var buffer = new StringBuilder();
            buffer.Append(firstLine);

            // The quotes are counted line by line; counting the whole buffer again after each line was quadratic.
            var quotes = CountQuotes(firstLine);

            while (true)
            {
                var nextLine = reader.ReadLine();
                if (nextLine == null)
                {
                    break;
                }

                buffer.Append('\n');
                buffer.Append(nextLine);
                quotes += CountQuotes(nextLine);

                if (quotes % 2 == 0)
                {
                    break;
                }
            }

            return buffer.ToString();
        }

        private static int CountQuotes(string line)
        {
            var count = 0;
            foreach (var ch in line)
            {
                if (ch == '"')
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Parses a logical CSV line into fields per RFC 4180.
        /// </summary>
        private static List<string> ParseLine(string line)
        {
            var fields = new List<string>();
            var i = 0;

            while (i <= line.Length)
            {
                if (i == line.Length)
                {
                    // Trailing comma means an empty field at end.
                    if (line.Length > 0 && line[^1] == ',')
                    {
                        fields.Add(string.Empty);
                    }

                    break;
                }

                if (line[i] == '"')
                {
                    // Quoted field.
                    var sb = new StringBuilder();
                    i++; // Skip opening quote.

                    while (i < line.Length)
                    {
                        if (line[i] == '"')
                        {
                            if (i + 1 < line.Length && line[i + 1] == '"')
                            {
                                // Escaped quote.
                                sb.Append('"');
                                i += 2;
                            }
                            else
                            {
                                // End of quoted field.
                                i++; // Skip closing quote.
                                break;
                            }
                        }
                        else
                        {
                            sb.Append(line[i]);
                            i++;
                        }
                    }

                    fields.Add(sb.ToString());

                    // Skip comma after quoted field.
                    if (i < line.Length && line[i] == ',')
                    {
                        i++;
                    }
                }
                else
                {
                    // Unquoted field.
                    var commaIndex = line.IndexOf(',', i);
                    if (commaIndex == -1)
                    {
                        fields.Add(line[i..]);
                        break;
                    }
                    else
                    {
                        fields.Add(line[i..commaIndex]);
                        i = commaIndex + 1;
                    }
                }
            }

            return fields;
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
            _writer.Flush();
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

                var value = values[i] ?? string.Empty;

                if (NeedsQuoting(value))
                {
                    _writer.Write('"');
                    _writer.Write(value.Replace("\"", "\"\""));
                    _writer.Write('"');
                }
                else
                {
                    _writer.Write(value);
                }
            }

            _writer.WriteLine();
        }

        private static bool NeedsQuoting(string value)
            => value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
    }
}
