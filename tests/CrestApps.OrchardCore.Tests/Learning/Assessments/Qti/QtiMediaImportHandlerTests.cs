using System.Data;
using System.Text.Json;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Learning.Assessments.ContentTransfer.Qti;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments.Qti;

public sealed class QtiMediaImportHandlerTests
{
    [Theory]
    [InlineData("items/images/sign.png", "qti-imports/entry1/items/images/sign.png")]
    [InlineData("images/my picture (1).png", "qti-imports/entry1/images/my-picture--1-.png")]
    [InlineData("images/../../evil.png", null)]
    [InlineData("", null)]
    public void MediaPath_IsUnderTheImportsFolder_WithSafeSegments(string packagePath, string expected)
    {
        Assert.Equal(expected, QtiMediaImportHandler.MediaPath("entry1", packagePath));
    }

    [Fact]
    public async Task TheImagesOfARow_AreStoredInTheMediaLibrary_AndThePromptPointsAtThem()
    {
        // Arrange
        var stored = new Dictionary<string, byte[]>();
        var store = new Mock<IMediaFileStore>();
        store.Setup(media => media.CreateFileFromStreamAsync(It.IsAny<string>(), It.IsAny<Stream>(), true))
            .Callback<string, Stream, bool>((path, stream, _) =>
            {
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                stored[path] = copy.ToArray();
            })
            .ReturnsAsync((string path, Stream _, bool _) => path);
        store.Setup(media => media.MapPathToPublicUrl(It.IsAny<string>())).Returns<string>(path => "/media/" + path);
        var handler = new QtiMediaImportHandler(store.Object, Options.Create(Media()), new Localizer());
        var context = Context(
            "<p><img src=\"qti-media:items/images/sign.png\" alt=\"Sign\" /></p>",
            [new QtiMediaCell { Path = "items/images/sign.png", Data = Convert.ToBase64String(QtiSamples.Png) }]);

        // Act
        await handler.ImportAsync(context);

        // Assert
        Assert.Empty(context.Errors);
        Assert.Equal(QtiSamples.Png, stored["qti-imports/entry1/items/images/sign.png"]);
        Assert.Equal("<p><img src=\"/media/qti-imports/entry1/items/images/sign.png\" alt=\"Sign\" /></p>", context.Row["QuestionPart_Prompt"]);
    }

    [Fact]
    public async Task AnImageTheMediaLibraryRefuses_FailsTheRow_AndNothingIsStored()
    {
        // Arrange
        var store = new Mock<IMediaFileStore>(MockBehavior.Strict);
        var options = Media();
        options.AllowedFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg" };
        var handler = new QtiMediaImportHandler(store.Object, Options.Create(options), new Localizer());
        var context = Context("<p><img src=\"qti-media:a.png\" /></p>", [new QtiMediaCell { Path = "a.png", Data = Convert.ToBase64String(QtiSamples.Png) }]);

        // Act
        await handler.ImportAsync(context);

        // Assert
        Assert.Single(context.Errors);
        Assert.Equal("Not imported from QTI", context.ErrorCategory);
    }

    private static MediaOptions Media()
        => new()
        {
            AllowedFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".gif", ".webp" },
            MaxFileSize = 30_000_000,
        };

    private static ContentImportContext Context(string prompt, QtiMediaCell[] media)
    {
        var table = new DataTable();
        table.Columns.Add("QuestionPart_Prompt");
        table.Columns.Add("QuestionPart_GeneralFeedback");
        table.Columns.Add(QtiColumns.Error);
        table.Columns.Add(QtiColumns.Media);
        var row = table.NewRow();
        row["QuestionPart_Prompt"] = prompt;
        row[QtiColumns.Media] = JsonSerializer.Serialize(media);

        return new ContentImportContext
        {
            ContentItem = new ContentItem { ContentType = "EssayQuestion" },
            Entry = new ContentTransferEntry { EntryId = "entry1", UploadedFileName = "bank.zip" },
            Columns = table.Columns,
            Row = row,
        };
    }

    private sealed class Localizer : IStringLocalizer<QtiMediaImportHandler>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
