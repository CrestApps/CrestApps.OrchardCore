using System.Data;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Learning.Assessments.ContentTransfer;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

public sealed class QuestionImportHandlerTests
{
    [Fact]
    public async Task AChoiceQuestionRow_ImportsItsOptionsAndRightAnswer_AndExportsBackTheSameCell()
    {
        // Arrange
        var handler = new ChoiceQuestionPartImportHandler(Localizer<ChoiceQuestionPartImportHandler>());
        var definition = new ContentTypePartDefinition(nameof(ChoiceQuestionPart), new ContentPartDefinition(nameof(ChoiceQuestionPart)), [])
        {
            ContentTypeDefinition = new ContentTypeDefinition("SingleChoiceQuestion", "Single choice question"),
        };
        var table = new DataTable();
        table.Columns.Add("ChoiceQuestionPart_Options");
        var row = table.NewRow();
        row["ChoiceQuestionPart_Options"] = "Foam|*CO2|Water";
        var question = new ContentItem { ContentType = "SingleChoiceQuestion" };

        // Act
        await handler.ImportAsync(new ContentPartImportMapContext { ContentItem = question, ContentTypePartDefinition = definition, Entry = new ContentTransferEntry(), Columns = table.Columns, Row = row });
        var exportRow = table.NewRow();
        await handler.ExportAsync(new ContentPartExportMapContext { ContentItem = question, ContentTypePartDefinition = definition, Row = exportRow });

        // Assert
        var part = question.Get<ChoiceQuestionPart>(nameof(ChoiceQuestionPart));
        Assert.Equal(["Foam", "CO2", "Water"], part.Options.Select(option => option.Text));
        Assert.Equal("CO2", Assert.Single(part.Options, option => option.Correct).Text);
        Assert.All(part.Options, option => Assert.False(string.IsNullOrEmpty(option.Id)));
        Assert.Equal("Foam|*CO2|Water", exportRow["ChoiceQuestionPart_Options"]);
    }

    private static IStringLocalizer<T> Localizer<T>()
        => new EchoLocalizer<T>();

    private sealed class EchoLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
