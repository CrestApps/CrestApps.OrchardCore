using System.Text;
using CrestApps.OrchardCore.AI.Documents.Services;

namespace CrestApps.OrchardCore.Tests.Helpers.DocumentReaders;

public sealed class PlainTextIngestionDocumentReaderTests
{
    private readonly PlainTextIngestionDocumentReader _reader = new();

    [Fact]
    public async Task ReadAsync_SimpleText_ExtractsContent()
    {
        using var stream = CreateStream("Hello, World!");

        var result = await _reader.ReadAsync(stream, "test.txt", "text/plain", TestContext.Current.CancellationToken);

        Assert.Single(result.Sections);
        Assert.Single(result.Sections[0].Elements);
        Assert.Equal("Hello, World!", result.Sections[0].Elements[0].Text);
    }

    [Fact]
    public async Task ReadAsync_MultilineText_ExtractsAllContent()
    {
        var content = "Line 1\nLine 2\nLine 3";
        using var stream = CreateStream(content);

        var result = await _reader.ReadAsync(stream, "test.txt", "text/plain", TestContext.Current.CancellationToken);

        Assert.Single(result.Sections);
        Assert.Single(result.Sections[0].Elements);
        Assert.Equal(content, result.Sections[0].Elements[0].Text);
    }

    [Fact]
    public async Task ReadAsync_EmptyContent_ReturnsNoSections()
    {
        using var stream = CreateStream("");

        var result = await _reader.ReadAsync(stream, "test.txt", "text/plain", TestContext.Current.CancellationToken);

        Assert.Empty(result.Sections);
    }

    [Fact]
    public async Task ReadAsync_WhitespaceOnly_ReturnsNoSections()
    {
        using var stream = CreateStream("   \n  \t  ");

        var result = await _reader.ReadAsync(stream, "test.txt", "text/plain", TestContext.Current.CancellationToken);

        Assert.Empty(result.Sections);
    }

    [Fact]
    public async Task ReadAsync_UnicodeContent_ExtractsCorrectly()
    {
        var content = "日本語テスト 🎉 Ñoño";
        using var stream = CreateStream(content);

        var result = await _reader.ReadAsync(stream, "test.txt", "text/plain", TestContext.Current.CancellationToken);

        Assert.Single(result.Sections);
        Assert.Equal(content, result.Sections[0].Elements[0].Text);
    }

    [Fact]
    public async Task ReadAsync_CsvContent_ExtractsAsPlainText()
    {
        var content = "Name,Age,City\nAlice,30,NY\nBob,25,LA";
        using var stream = CreateStream(content);

        var result = await _reader.ReadAsync(stream, "data.csv", "text/csv", TestContext.Current.CancellationToken);

        Assert.Single(result.Sections);
        Assert.Equal(content, result.Sections[0].Elements[0].Text);
    }

    [Fact]
    public async Task ReadAsync_JsonContent_ExtractsAsPlainText()
    {
        var content = "{\"key\": \"value\", \"number\": 42}";
        using var stream = CreateStream(content);

        var result = await _reader.ReadAsync(stream, "data.json", "application/json", TestContext.Current.CancellationToken);

        Assert.Single(result.Sections);
        Assert.Equal(content, result.Sections[0].Elements[0].Text);
    }

    [Fact]
    public async Task ReadAsync_SetsIdentifier()
    {
        using var stream = CreateStream("content");

        var result = await _reader.ReadAsync(stream, "my-document.txt", "text/plain", TestContext.Current.CancellationToken);

        Assert.Equal("my-document.txt", result.Identifier);
    }

    private static MemoryStream CreateStream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }
}
