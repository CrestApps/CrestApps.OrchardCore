using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.ContentTransfer.Qti;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments.Qti;

public sealed class QtiFileFormatProviderTests
{
    [Fact]
    public void TheFormats_ImportQuestionTypesOnly_AndNeverWrite()
    {
        // Arrange
        IContentTransferFileFormatProvider package = new QtiPackageFileFormatProvider(QuestionTypes(), new EchoLocalizer());
        IContentTransferFileFormatProvider item = new QtiItemFileFormatProvider(QuestionTypes(), new EchoLocalizer());

        // Act and assert
        Assert.True(package.CanHandle("Bank.ZIP"));
        Assert.False(package.CanHandle("bank.xml"));
        Assert.True(item.CanHandle("item.xml"));
        Assert.True(package.CanImport(AssessmentsConstants.ContentTypes.EssayQuestion));
        Assert.False(package.CanImport("Article"));
        Assert.False(package.CanExport);
        Assert.False(item.CanExport);
        Assert.NotNull(package.DescribeImport(new ContentTypeDefinition(AssessmentsConstants.ContentTypes.EssayQuestion, "Essay question")));
        Assert.Null(package.DescribeImport(new ContentTypeDefinition("Article", "Article")));
    }

    [Fact]
    public void TheReader_WritesARowPerItem_WithItsContentTypeAndIdentifier_AndTheReasonARefusedItemIsNotImported()
    {
        // Arrange
        var unsupported = QtiSamples.Item21("hotspot", "Hotspot", """<responseDeclaration identifier="RESPONSE" cardinality="single" baseType="identifier"/>""", """<hotspotInteraction responseIdentifier="RESPONSE"/>""", string.Empty);
        using var package = QtiSamples.Package(
            QtiSamples.Manifest21(("choice", "choice.xml"), ("hotspot", "hotspot.xml")),
            [("choice.xml", QtiSamples.Choice21()), ("hotspot.xml", unsupported)],
            ("images/sign.png", QtiSamples.Png));
        var provider = new QtiPackageFileFormatProvider(QuestionTypes(), new EchoLocalizer());

        // Act
        using var reader = provider.CreateReader(package);
        var columns = reader.GetColumnNames();
        var rows = reader.ReadRows().ToList();

        // Assert
        Assert.Equal(2, reader.GetRowCount());
        Assert.Equal(2, rows.Count);
        string Cell(int row, string column) => rows[row][columns.ToList().IndexOf(column)];
        Assert.Equal(AssessmentsConstants.ContentTypes.SingleChoiceQuestion, Cell(0, ContentTransferColumns.ContentType));
        Assert.Equal("choice", Cell(0, "QuestionPart_SourceIdentifier"));
        Assert.Equal("choice.xml", Cell(0, "QuestionPart_SourceFile"));
        Assert.Equal("Unattended Luggage", Cell(0, "TitlePart_Title"));
        Assert.True(string.IsNullOrEmpty(Cell(0, QtiColumns.Error)));
        Assert.Contains("hotspot", Cell(1, QtiColumns.Error), StringComparison.Ordinal);
        Assert.Contains("hotspotInteraction", Cell(1, QtiColumns.Error), StringComparison.Ordinal);
        Assert.True(string.IsNullOrEmpty(Cell(1, ContentTransferColumns.ContentType)));
    }

    [Fact]
    public void AFileThatIsNotAQtiPackage_IsOneRowWithTheReason()
    {
        // Arrange
        using var zip = Scorm.PackageZip.SingleScoPackage();
        var provider = new QtiPackageFileFormatProvider(QuestionTypes(), new EchoLocalizer());

        // Act
        using var reader = provider.CreateReader(zip);
        var columns = reader.GetColumnNames().ToList();
        var row = Assert.Single(reader.ReadRows());

        // Assert
        Assert.False(string.IsNullOrEmpty(row[columns.IndexOf(QtiColumns.Error)]));
    }

    internal static IOptions<QuestionTypeOptions> QuestionTypes()
    {
        var options = new QuestionTypeOptions();
        (string Name, string Type)[] types =
        [
            (AssessmentsConstants.QuestionTypes.SingleChoice, AssessmentsConstants.ContentTypes.SingleChoiceQuestion),
            (AssessmentsConstants.QuestionTypes.MultipleChoice, AssessmentsConstants.ContentTypes.MultipleChoiceQuestion),
            (AssessmentsConstants.QuestionTypes.TrueFalse, AssessmentsConstants.ContentTypes.TrueFalseQuestion),
            (AssessmentsConstants.QuestionTypes.ShortText, AssessmentsConstants.ContentTypes.ShortTextQuestion),
            (AssessmentsConstants.QuestionTypes.Numeric, AssessmentsConstants.ContentTypes.NumericQuestion),
            (AssessmentsConstants.QuestionTypes.Matching, AssessmentsConstants.ContentTypes.MatchingQuestion),
            (AssessmentsConstants.QuestionTypes.Ordering, AssessmentsConstants.ContentTypes.OrderingQuestion),
            (AssessmentsConstants.QuestionTypes.FillInBlanks, AssessmentsConstants.ContentTypes.FillInBlanksQuestion),
            (AssessmentsConstants.QuestionTypes.Essay, AssessmentsConstants.ContentTypes.EssayQuestion),
        ];

        foreach (var (name, type) in types)
        {
            options.Add(new QuestionTypeDescriptor(name) { ContentType = type });
        }

        return Options.Create(options);
    }

    internal sealed class EchoLocalizer : IStringLocalizer<QtiFileFormatProvider>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
