using System.Data;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.FileFormats;
using CrestApps.OrchardCore.ContentTransfer.Services;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContentTransfer;

public sealed class ContentImportRowTypesTests
{
    [Fact]
    public void Collect_PutsTheImportedTypeFirst_AndAddsTheTypesItsProvidersGroupWithIt()
    {
        // Arrange
        var providers = new IContentImportRowTypeProvider[]
        {
            new GroupProvider(["SingleChoice", "Essay", "SingleChoice"]),
            new GroupProvider(["Product"]),
        };

        // Act
        var questions = ContentImportRowTypes.Collect(providers, new ContentTypeDefinition("Essay", "Essay"));
        var article = ContentImportRowTypes.Collect(providers, new ContentTypeDefinition("Article", "Article"));

        // Assert
        Assert.Equal(["Essay", "SingleChoice"], questions);
        Assert.Equal(["Article"], article);
    }

    [Fact]
    public void Resolve_ReadsTheRowsType_OnlyWhenTheImportAllowsSeveral()
    {
        // Arrange
        var table = new DataTable();
        table.Columns.Add(ContentTransferColumns.ContentType);
        var essay = Row(table, "Essay");
        var empty = Row(table, null);
        var product = Row(table, "Product");
        string[] group = ["SingleChoice", "Essay"];

        // Act and assert
        Assert.Equal("Essay", ContentImportRowTypes.Resolve(essay, "SingleChoice", group, out _));
        Assert.Equal("SingleChoice", ContentImportRowTypes.Resolve(empty, "SingleChoice", group, out _));
        Assert.Null(ContentImportRowTypes.Resolve(product, "SingleChoice", group, out var named));
        Assert.Equal("Product", named);

        // A file of a type with no group keeps the column as any other column: every row is the imported type.
        Assert.Equal("Article", ContentImportRowTypes.Resolve(product, "Article", [], out named));
        Assert.Null(named);
    }

    [Fact]
    public void Resolve_IgnoresAFileWithoutTheColumn()
    {
        // Arrange
        var table = new DataTable();
        table.Columns.Add("Title");
        var row = table.NewRow();

        // Act
        var type = ContentImportRowTypes.Resolve(row, "SingleChoice", ["SingleChoice", "Essay"], out var named);

        // Assert
        Assert.Equal("SingleChoice", type);
        Assert.Null(named);
    }

    [Fact]
    public void ResolveImportFormat_PicksTheFormatThatImportsTheContentType_WhenTwoShareAnExtension()
    {
        // Arrange
        var csv = new CsvContentTransferFileFormatProvider();
        var questionsZip = new ZipFormat("Question");
        var productsZip = new ZipFormat("Product");
        IContentTransferFileFormatProvider[] providers = [csv, questionsZip, productsZip];

        // Act and assert
        Assert.Same(questionsZip, ContentImportRowTypes.ResolveImportFormat(providers, "bank.zip", "Question"));
        Assert.Same(productsZip, ContentImportRowTypes.ResolveImportFormat(providers, "feed.ZIP", "Product"));
        Assert.Null(ContentImportRowTypes.ResolveImportFormat(providers, "a.zip", "Article"));
        Assert.Same(csv, ContentImportRowTypes.ResolveImportFormat(providers, "a.csv", "Article"));
        Assert.True(((IContentTransferFileFormatProvider)csv).CanExport);
        Assert.True(((IContentTransferFileFormatProvider)csv).CanImport("Anything"));
    }

    private static DataRow Row(DataTable table, string type)
    {
        var row = table.NewRow();
        row[ContentTransferColumns.ContentType] = (object)type ?? DBNull.Value;

        return row;
    }

    private sealed class GroupProvider(string[] group) : IContentImportRowTypeProvider
    {
        public IEnumerable<string> GetRowContentTypes(ContentTypeDefinition contentType)
            => group.Contains(contentType.Name) ? group : [];
    }

    private sealed class ZipFormat(string contentType) : IContentTransferFileFormatProvider
    {
        public string FileExtension => ".zip";

        public string ContentType => "application/zip";

        public bool CanExport => false;

        public bool CanHandle(string fileName) => fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

        public bool CanImport(string type) => type == contentType;

        public IContentTransferFileReader CreateReader(Stream stream) => throw new NotSupportedException();

        public IContentTransferFileWriter CreateWriter(Stream stream, string sheetName) => throw new NotSupportedException();
    }
}
