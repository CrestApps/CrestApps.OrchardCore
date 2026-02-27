using CrestApps.OrchardCore.AI.Documents.Pdf;
using UglyToad.PdfPig.Writer;

namespace CrestApps.OrchardCore.Tests.Helpers.DocumentReaders;

public sealed class PdfIngestionDocumentReaderTests
{
    private readonly PdfIngestionDocumentReader _reader = new();

    [Fact]
    public async Task ReadAsync_SinglePagePdf_ExtractsText()
    {
        using var stream = CreatePdfWithText("Hello World from PDF");

        var result = await _reader.ReadAsync(stream, "test.pdf", "application/pdf", TestContext.Current.CancellationToken);

        Assert.NotEmpty(result.Sections);

        var allText = string.Join(" ", result.Sections.SelectMany(s => s.Elements).Select(e => e.Text));
        Assert.Contains("Hello World from PDF", allText);
    }

    [Fact]
    public async Task ReadAsync_MultiPagePdf_ExtractsAllPages()
    {
        using var stream = CreateMultiPagePdf("Page one content", "Page two content");

        var result = await _reader.ReadAsync(stream, "test.pdf", "application/pdf", TestContext.Current.CancellationToken);

        // Should have sections for pages with text.
        Assert.True(result.Sections.Count >= 1);

        var allText = string.Join(" ", result.Sections.SelectMany(s => s.Elements).Select(e => e.Text));
        Assert.Contains("Page one content", allText);
        Assert.Contains("Page two content", allText);
    }

    [Fact]
    public async Task ReadAsync_EmptyPdf_ReturnsEmptyDocument()
    {
        using var stream = CreateEmptyPdf();

        var result = await _reader.ReadAsync(stream, "test.pdf", "application/pdf", TestContext.Current.CancellationToken);

        // Empty PDF may have sections with zero elements or no sections.
        var totalElements = result.Sections.Sum(s => s.Elements.Count);
        Assert.Equal(0, totalElements);
    }

    [Fact]
    public async Task ReadAsync_UnsupportedMediaType_ThrowsNotSupportedException()
    {
        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _reader.ReadAsync(stream, "test.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadAsync_SetsIdentifier()
    {
        using var stream = CreatePdfWithText("Test");

        var result = await _reader.ReadAsync(stream, "my-document.pdf", "application/pdf", TestContext.Current.CancellationToken);

        Assert.Equal("my-document.pdf", result.Identifier);
    }

    [Fact]
    public async Task ReadAsync_NonSeekableStream_ExtractsCorrectly()
    {
        using var seekableStream = CreatePdfWithText("Non-seekable test");
        using var nonSeekable = new NonSeekableStream(seekableStream);

        var result = await _reader.ReadAsync(nonSeekable, "test.pdf", "application/pdf", TestContext.Current.CancellationToken);

        Assert.NotEmpty(result.Sections);

        var allText = string.Join(" ", result.Sections.SelectMany(s => s.Elements).Select(e => e.Text));
        Assert.Contains("Non-seekable test", allText);
    }

    #region Helpers

    private static MemoryStream CreatePdfWithText(string text)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842); // A4 size.
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
        page.AddText(text, 12, new UglyToad.PdfPig.Core.PdfPoint(72, 720), font);

        var bytes = builder.Build();
        return new MemoryStream(bytes);
    }

    private static MemoryStream CreateMultiPagePdf(params string[] pageTexts)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);

        foreach (var text in pageTexts)
        {
            var page = builder.AddPage(595, 842);
            page.AddText(text, 12, new UglyToad.PdfPig.Core.PdfPoint(72, 720), font);
        }

        var bytes = builder.Build();
        return new MemoryStream(bytes);
    }

    private static MemoryStream CreateEmptyPdf()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(595, 842); // Add empty page.

        var bytes = builder.Build();
        return new MemoryStream(bytes);
    }

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
