using System.Text;
using CrestApps.OrchardCore.ContentTransfer.FileFormats;

namespace CrestApps.OrchardCore.Tests.Modules.ContentTransfer;

public sealed class CsvContentTransferFileFormatProviderTests
{
    [Fact]
    public void CanHandle_WithCsvFile_ShouldReturnTrue()
    {
        var provider = new CsvContentTransferFileFormatProvider();

        var result = provider.CanHandle("items.csv");

        Assert.True(result);
    }

    [Fact]
    public void CreateReaderAndWriter_ShouldRoundTripCsvContent()
    {
        var provider = new CsvContentTransferFileFormatProvider();
        using var stream = new MemoryStream();

        using (var writer = provider.CreateWriter(stream, "Ignored"))
        {
            writer.WriteHeader(["Title", "Description"]);
            writer.WriteRow(["Item 1", "Line 1,\nLine 2"]);
            writer.Flush();
        }

        stream.Position = 0;

        using var reader = provider.CreateReader(stream);
        var columns = reader.GetColumnNames();
        var rows = reader.ReadRows().ToList();

        Assert.Equal(["Title", "Description"], columns);
        Assert.Single(rows);
        Assert.Equal("Item 1", rows[0][0]);
        Assert.Equal("Line 1,\nLine 2", rows[0][1]);
    }

    [Fact]
    public void CreateReader_CountsAndReadsRowsAgain_FromASeekableStream()
    {
        // Arrange
        var provider = new CsvContentTransferFileFormatProvider();
        using var stream = new CountingStream(Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("Name,Notes\nAda,\"line one\nline \"\"two\"\"\"\nGrace,\n\nAlan,last\n"))
            .ToArray());

        // Act
        using var reader = provider.CreateReader(stream);
        var count = reader.GetRowCount();
        var rows = reader.ReadRows().ToList();
        var rowsAgain = reader.ReadRows().ToList();

        // Assert
        Assert.Equal(["Name", "Notes"], reader.GetColumnNames());
        Assert.Equal(4, count);
        Assert.Equal(4, rows.Count);
        Assert.Equal("Ada", rows[0][0]);
        Assert.Equal("line one\nline \"two\"", rows[0][1]);
        Assert.Equal("Grace", rows[1][0]);
        Assert.Null(rows[2][0]);
        Assert.Equal(["Alan", "last"], rows[3]);
        Assert.Equal(4, rowsAgain.Count);
        Assert.Equal("Alan", rowsAgain[3][0]);
    }

    [Fact]
    public void CreateReader_DoesNotReadTheRowsAhead()
    {
        // Arrange
        // Every row used to be read into memory when the reader was created, and each run of a long import created
        // a reader, so a large file was held in memory for as long as the run lasted.
        var provider = new CsvContentTransferFileFormatProvider();
        var content = new StringBuilder("Name,Phone\n");

        for (var i = 0; i < 20_000; i++)
        {
            content.Append("Lead ").Append(i).Append(",555-000-").Append(i.ToString("D4")).Append('\n');
        }

        using var stream = new CountingStream(Encoding.UTF8.GetBytes(content.ToString()));

        // Act
        using var reader = provider.CreateReader(stream);
        var readByCreation = stream.BytesRead;
        var count = reader.GetRowCount();
        string[] first = null;
        var read = 0;

        foreach (var row in reader.ReadRows())
        {
            first ??= row;
            read++;
        }

        // Assert
        Assert.True(readByCreation < stream.Length / 2, $"Creating the reader read {readByCreation} of {stream.Length} bytes.");
        Assert.Equal(20_000, count);
        Assert.Equal(20_000, read);
        Assert.Equal(["Lead 0", "555-000-0000"], first);
    }

    [Fact]
    public void CreateReader_ReadsAStreamThatCannotSeek()
    {
        // Arrange
        var provider = new CsvContentTransferFileFormatProvider();
        using var stream = new CountingStream(Encoding.UTF8.GetBytes("Name\nAda\nGrace\n"), canSeek: false);

        // Act
        using var reader = provider.CreateReader(stream);
        var count = reader.GetRowCount();
        var rows = reader.ReadRows().Select(row => row[0]).ToList();

        // Assert
        Assert.Equal(2, count);
        Assert.Equal(["Ada", "Grace"], rows);
    }

    private sealed class CountingStream(byte[] buffer, bool canSeek = true) : MemoryStream(buffer)
    {
        public long BytesRead { get; private set; }

        public override bool CanSeek => canSeek && base.CanSeek;

        public override long Position
        {
            get => base.Position;
            set
            {
                if (!canSeek)
                {
                    throw new NotSupportedException();
                }

                base.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = base.Read(buffer, offset, count);
            BytesRead += read;

            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = base.Read(buffer);
            BytesRead += read;

            return read;
        }
    }
}
