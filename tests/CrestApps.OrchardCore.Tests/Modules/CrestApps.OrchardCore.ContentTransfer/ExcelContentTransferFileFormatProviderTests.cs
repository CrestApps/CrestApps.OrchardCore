using CrestApps.OrchardCore.ContentTransfer.OpenXml.Services;

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
}
