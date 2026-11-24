using System.Data;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.ContentTransfer.Qti;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments.Qti;

/// <summary>
/// Review round 9, M8: only the QTI reader's rows may write media. A spreadsheet row with a <c>QtiMedia</c> column
/// writes nothing, and an image of a QTI package must be one of the parser's image types, with the bytes of one.
/// </summary>
public sealed class QtiMediaRound9Tests
{
    private const string Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert(document.cookie)\"/>";

    [Fact]
    public async Task Round9M8_ASpreadsheetRowWithAQtiMediaColumn_WritesNothingToTheMediaLibrary()
    {
        var (handler, stored) = Handler();
        var context = Context("questions.csv", "<p><img src=\"qti-media:x.svg\" /></p>", "x.svg", Encoding.UTF8.GetBytes(Svg));

        await handler.ImportAsync(context);

        Assert.Empty(stored);
    }

    [Fact]
    public async Task Round9M8_AQtiImageOutsideTheImageTypes_OrWithoutTheBytesOfOne_IsRefused()
    {
        var (handler, stored) = Handler();
        var svg = Context("bank.zip", "<p><img src=\"qti-media:x.svg\" /></p>", "x.svg", Encoding.UTF8.GetBytes(Svg));
        var disguised = Context("bank.zip", "<p><img src=\"qti-media:x.png\" /></p>", "x.png", Encoding.UTF8.GetBytes(Svg));
        var real = Context("bank.zip", "<p><img src=\"qti-media:y.png\" /></p>", "y.png", QtiSamples.Png);

        await handler.ImportAsync(svg);
        await handler.ImportAsync(disguised);
        await handler.ImportAsync(real);

        Assert.NotEmpty(svg.Errors);
        Assert.NotEmpty(disguised.Errors);
        Assert.Empty(real.Errors);
        Assert.Equal(["qti-imports/entry1/y.png"], stored.Keys);
    }

    private static (IContentImportHandler Handler, Dictionary<string, byte[]> Stored) Handler()
    {
        var stored = new Dictionary<string, byte[]>();
        var store = new Mock<IMediaFileStore>();
        store.Setup(media => media.CreateFileFromStreamAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<bool>()))
            .Callback<string, Stream, bool>((path, stream, _) =>
            {
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                stored[path] = copy.ToArray();
            })
            .ReturnsAsync((string path, Stream _, bool _) => path);
        store.Setup(media => media.MapPathToPublicUrl(It.IsAny<string>())).Returns<string>(path => "/media/" + path);

        var services = new ServiceCollection()
            .AddSingleton(store.Object)
            .AddSingleton(Mock.Of<CrestApps.OrchardCore.FileStorage.IProtectedFileStore<CrestApps.OrchardCore.Learning.Assessments.Core.Qti.QtiFeedbackImages>>(MockBehavior.Strict))
            .AddSingleton(new global::OrchardCore.Environment.Shell.ShellSettings())
            .AddSingleton(Options.Create(new MediaOptions
            {
                AllowedFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".svg" },
                MaxFileSize = 30_000_000,
            }))
            .AddSingleton(Options.Create(new QuestionTypeOptions()))
            .AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughLocalizer<>))
            .AddSingleton<IContentTransferFileFormatProvider, QtiPackageFileFormatProvider>()
            .BuildServiceProvider();

        return (ActivatorUtilities.CreateInstance<QtiMediaImportHandler>(services), stored);
    }

    private static ContentImportContext Context(string fileName, string prompt, string path, byte[] content)
    {
        var table = new DataTable();
        table.Columns.Add("QuestionPart_Prompt");
        table.Columns.Add("QuestionPart_GeneralFeedback");
        table.Columns.Add(QtiColumns.Error);
        table.Columns.Add(QtiColumns.Media);
        var row = table.NewRow();
        row["QuestionPart_Prompt"] = prompt;
        row[QtiColumns.Media] = JsonSerializer.Serialize(new[] { new QtiMediaCell { Path = path, Data = Convert.ToBase64String(content) } });

        return new ContentImportContext
        {
            ContentItem = new ContentItem { ContentType = "EssayQuestion" },
            Entry = new ContentTransferEntry { EntryId = "entry1", UploadedFileName = fileName, StoredFileName = fileName },
            Columns = table.Columns,
            Row = row,
        };
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
