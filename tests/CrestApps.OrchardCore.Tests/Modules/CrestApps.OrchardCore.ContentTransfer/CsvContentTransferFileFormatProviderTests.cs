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
}
