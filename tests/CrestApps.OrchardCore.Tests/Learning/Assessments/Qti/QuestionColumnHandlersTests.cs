using System.Data;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.ContentTransfer;
using CrestApps.OrchardCore.Learning.Assessments.ContentTransfer.Qti;
using CrestApps.OrchardCore.Learning.Assessments.Core.Qti;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Infrastructure.Html;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments.Qti;

public sealed class QuestionColumnHandlersTests
{
    [Fact]
    public async Task AMatchingRow_ImportsItsPairs_AndExportsBackTheSameCell()
    {
        // Arrange
        var handler = new MatchingQuestionPartImportHandler(new Localizer<MatchingQuestionPartImportHandler>());
        var (question, table, row) = Row(AssessmentsConstants.ContentTypes.MatchingQuestion, ("MatchingQuestionPart_Pairs", "France=Paris|2 \\= 1+1=True|Germany=Berlin"), ("MatchingQuestionPart_PartialCredit", "no"));

        // Act
        await handler.ImportAsync(Context<MatchingQuestionPart>(question, table, row));
        var export = await Export<MatchingQuestionPart>(handler, question, table);

        // Assert
        var part = question.Get<MatchingQuestionPart>(nameof(MatchingQuestionPart));
        Assert.Equal([("France", "Paris"), ("2 = 1+1", "True"), ("Germany", "Berlin")], part.Pairs.Select(pair => (pair.Prompt, pair.Answer)));
        Assert.All(part.Pairs, pair => Assert.False(string.IsNullOrEmpty(pair.Id)));
        Assert.False(part.PartialCredit);
        Assert.Equal("France=Paris|2 \\= 1+1=True|Germany=Berlin", export["MatchingQuestionPart_Pairs"]);
    }

    [Fact]
    public async Task AnOrderingRow_ImportsItsItemsInTheirOrder()
    {
        // Arrange
        var handler = new OrderingQuestionPartImportHandler(new Localizer<OrderingQuestionPartImportHandler>());
        var (question, table, row) = Row(AssessmentsConstants.ContentTypes.OrderingQuestion, ("OrderingQuestionPart_Items", "Plan|Do|Check|Act"), ("OrderingQuestionPart_PartialCredit", "yes"));

        // Act
        await handler.ImportAsync(Context<OrderingQuestionPart>(question, table, row));
        var export = await Export<OrderingQuestionPart>(handler, question, table);

        // Assert
        var part = question.Get<OrderingQuestionPart>(nameof(OrderingQuestionPart));
        Assert.Equal(["Plan", "Do", "Check", "Act"], part.Items.Select(item => item.Text));
        Assert.Equal(4, part.Items.Select(item => item.Id).Distinct().Count());
        Assert.True(part.PartialCredit);
        Assert.Equal("Plan|Do|Check|Act", export["OrderingQuestionPart_Items"]);
    }

    [Fact]
    public async Task AFillInTheBlanksRow_ImportsItsText()
    {
        // Arrange
        var handler = new FillInBlanksQuestionPartImportHandler(new Localizer<FillInBlanksQuestionPartImportHandler>());
        var (question, table, row) = Row(AssessmentsConstants.ContentTypes.FillInBlanksQuestion, ("FillInBlanksQuestionPart_Text", "Water boils at [[100|one hundred]] degrees."), ("FillInBlanksQuestionPart_CaseSensitive", "yes"));

        // Act
        await handler.ImportAsync(Context<FillInBlanksQuestionPart>(question, table, row));

        // Assert
        var part = question.Get<FillInBlanksQuestionPart>(nameof(FillInBlanksQuestionPart));
        Assert.Equal("Water boils at [[100|one hundred]] degrees.", part.Text);
        Assert.True(part.CaseSensitive);
    }

    [Fact]
    public async Task ChoiceOptions_KeepEscapedCharacters_AndTheirFeedback()
    {
        // Arrange
        var handler = new ChoiceQuestionPartImportHandler(new Localizer<ChoiceQuestionPartImportHandler>());
        var (question, table, row) = Row(AssessmentsConstants.ContentTypes.SingleChoiceQuestion, ("ChoiceQuestionPart_Options", "\\*Starred|*Either \\| or|C:\\temp"), ("ChoiceQuestionPart_OptionFeedback", "|Right.|"));

        // Act
        await handler.ImportAsync(Context<ChoiceQuestionPart>(question, table, row));
        var export = await Export<ChoiceQuestionPart>(handler, question, table);

        // Assert
        var part = question.Get<ChoiceQuestionPart>(nameof(ChoiceQuestionPart));
        Assert.Equal(["*Starred", "Either | or", "C:\\temp"], part.Options.Select(option => option.Text));
        Assert.Equal([false, true, false], part.Options.Select(option => option.Correct));
        Assert.Equal([null, "Right.", null], part.Options.Select(option => option.Feedback));
        Assert.Equal("\\*Starred|*Either \\| or|C:\\\\temp", export["ChoiceQuestionPart_Options"]);
    }

    [Fact]
    public async Task TheQuestionPart_SanitizesThePromptAndFeedback_AndKeepsTheSource()
    {
        // Arrange
        var sanitizer = new Mock<IHtmlSanitizerService>();
        sanitizer.Setup(service => service.Sanitize(It.IsAny<string>())).Returns<string>(html => html.Replace("<script>x</script>", string.Empty, StringComparison.Ordinal));
        var handler = new QuestionPartImportHandler(sanitizer.Object, new Localizer<QuestionPartImportHandler>());
        var (question, table, row) = Row(AssessmentsConstants.ContentTypes.EssayQuestion,
            ("QuestionPart_Prompt", "<p>Hi</p><script>x</script>"),
            ("QuestionPart_GeneralFeedback", "<p>Well</p><script>x</script>"),
            ("QuestionPart_SourceIdentifier", "essay"),
            ("QuestionPart_SourceFile", "bank.zip/items/essay.xml"));

        // Act
        await handler.ImportAsync(Context<QuestionPart>(question, table, row));

        // Assert
        var part = question.Get<QuestionPart>(nameof(QuestionPart));
        Assert.Equal("<p>Hi</p>", part.Prompt);
        Assert.Equal("<p>Well</p>", part.GeneralFeedback);
        Assert.Equal("essay", part.SourceIdentifier);
        Assert.Equal("bank.zip/items/essay.xml", part.SourceFile);
    }

    [Fact]
    public async Task TheQtiHandler_FailsARowWithAnError_AndNamesTheUploadedFileInTheSource()
    {
        // Arrange
        var handler = new QtiQuestionImportHandler(new Localizer<QtiQuestionImportHandler>());
        var entry = new ContentTransferEntry { UploadedFileName = "bank.zip" };
        var (refusedItem, refusedTable, refusedRow) = Row(AssessmentsConstants.ContentTypes.EssayQuestion, (QtiColumns.Error, "Item hotspot (hotspot.xml) was not imported."));
        var (item, table, row) = Row(AssessmentsConstants.ContentTypes.EssayQuestion, ("QuestionPart_SourceFile", "items/essay.xml"), (QtiColumns.Error, null));
        var refused = new ContentImportContext { ContentItem = refusedItem, Columns = refusedTable.Columns, Row = refusedRow, Entry = entry, ContentTypeDefinition = new ContentTypeDefinition(AssessmentsConstants.ContentTypes.EssayQuestion, "Essay") };
        var imported = new ContentImportContext { ContentItem = item, Columns = table.Columns, Row = row, Entry = entry, ContentTypeDefinition = new ContentTypeDefinition(AssessmentsConstants.ContentTypes.EssayQuestion, "Essay") };

        // Act
        await handler.ImportAsync(refused);
        await handler.ImportAsync(imported);

        // Assert
        Assert.Equal(["Item hotspot (hotspot.xml) was not imported."], refused.Errors);
        Assert.Equal("Not imported from QTI", refused.ErrorCategory);
        Assert.Empty(imported.Errors);
        Assert.Equal("bank.zip/items/essay.xml", row["QuestionPart_SourceFile"]);
    }

    [Fact]
    public void QuestionTypes_AreOneGroupOfRowTypes()
    {
        // Arrange
        var provider = new QuestionRowTypeProvider(QtiFileFormatProviderTests.QuestionTypes());

        // Act
        var essay = provider.GetRowContentTypes(new ContentTypeDefinition(AssessmentsConstants.ContentTypes.EssayQuestion, "Essay")).ToList();
        var article = provider.GetRowContentTypes(new ContentTypeDefinition("Article", "Article")).ToList();

        // Assert
        Assert.Equal(9, essay.Count);
        Assert.Contains(AssessmentsConstants.ContentTypes.MatchingQuestion, essay);
        Assert.Empty(article);
    }

    [Fact]
    public async Task AQtiMatchingItem_ReadThroughTheColumnHandlers_BecomesTheMatchingQuestion()
    {
        // Arrange
        var item = QtiSamples.Item21("match", "Capitals", """
            <responseDeclaration identifier="RESPONSE" cardinality="multiple" baseType="directedPair">
              <correctResponse><value>FR PAR</value><value>DE BER</value></correctResponse>
            </responseDeclaration>
            """, """
            <matchInteraction responseIdentifier="RESPONSE" maxAssociations="2">
              <prompt>Match each country to its capital.</prompt>
              <simpleMatchSet>
                <simpleAssociableChoice identifier="FR" matchMax="1">France = FR</simpleAssociableChoice>
                <simpleAssociableChoice identifier="DE" matchMax="1">Germany | DE</simpleAssociableChoice>
              </simpleMatchSet>
              <simpleMatchSet>
                <simpleAssociableChoice identifier="BER" matchMax="1">Berlin</simpleAssociableChoice>
                <simpleAssociableChoice identifier="PAR" matchMax="1">Paris</simpleAssociableChoice>
              </simpleMatchSet>
            </matchInteraction>
            """, $"""<responseProcessing template="{QtiSamples.MatchCorrect21}"/>""");
        using var file = QtiSamples.Xml(item);
        var rows = QtiQuestionRows.Write(QtiReader.Read(file, "match.xml"), QtiFileFormatProviderTests.QuestionTypes().Value, new Localizer<QtiFileFormatProvider>());
        var table = new DataTable();

        foreach (var column in QtiQuestionRows.Columns)
        {
            table.Columns.Add(column);
        }

        var row = table.NewRow();
        row.ItemArray = Assert.Single(rows);
        var question = new ContentItem { ContentType = AssessmentsConstants.ContentTypes.MatchingQuestion };
        var handler = new MatchingQuestionPartImportHandler(new Localizer<MatchingQuestionPartImportHandler>());

        // Act
        await handler.ImportAsync(Context<MatchingQuestionPart>(question, table, row));

        // Assert
        Assert.Equal(AssessmentsConstants.ContentTypes.MatchingQuestion, row[ContentTransferColumns.ContentType]);
        var part = question.Get<MatchingQuestionPart>(nameof(MatchingQuestionPart));
        Assert.Equal([("France = FR", "Paris"), ("Germany | DE", "Berlin")], part.Pairs.Select(pair => (pair.Prompt, pair.Answer)));
    }

    private static (ContentItem Question, DataTable Table, DataRow Row) Row(string contentType, params (string Column, string Value)[] cells)
    {
        var table = new DataTable();

        foreach (var (column, _) in cells)
        {
            table.Columns.Add(column);
        }

        var row = table.NewRow();

        foreach (var (column, value) in cells)
        {
            row[column] = (object)value ?? DBNull.Value;
        }

        return (new ContentItem { ContentType = contentType }, table, row);
    }

    private static ContentPartImportMapContext Context<TPart>(ContentItem question, DataTable table, DataRow row)
        => new()
        {
            ContentItem = question,
            ContentTypePartDefinition = Definition<TPart>(question.ContentType),
            Entry = new ContentTransferEntry(),
            Columns = table.Columns,
            Row = row,
        };

    private static async Task<DataRow> Export<TPart>(IContentPartImportHandler handler, ContentItem question, DataTable table)
    {
        var export = table.NewRow();
        await handler.ExportAsync(new ContentPartExportMapContext { ContentItem = question, ContentTypePartDefinition = Definition<TPart>(question.ContentType), Row = export });

        return export;
    }

    private static ContentTypePartDefinition Definition<TPart>(string contentType)
        => new(typeof(TPart).Name, new ContentPartDefinition(typeof(TPart).Name), [])
        {
            ContentTypeDefinition = new ContentTypeDefinition(contentType, contentType),
        };

    private sealed class Localizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
