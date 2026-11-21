using System.Text;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Imports;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tabular;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CrestApps.OrchardCore.Tests.Surveys.Banks;

/// <summary>
/// Reading bank questions from CSV and Excel spreadsheets.
/// </summary>
public sealed class SpreadsheetQuestionReaderTests
{
    private const string Header = "Id,Type,Question,Choices,Correct,Points,Tags,Difficulty,Feedback";

    [Fact]
    public void Read_BuildsAChoiceQuestionWithItsKeyPointsTagsDifficultyAndFeedback()
    {
        // Arrange
        var csv = Header + "\r\nq-1,SingleChoice,Capital of France?,Paris|London|Rome,Paris,2,\"geo, europe\",4,Paris is the capital.";

        // Act
        var result = Read(csv);

        // Assert
        Assert.Empty(result.Errors);
        var bankQuestion = Assert.Single(result.Questions);
        var question = bankQuestion.Question;
        Assert.Equal("q-1", bankQuestion.SourceIdentifier);
        Assert.Equal(SurveyControlNames.SingleChoice, question.ControlType);
        Assert.Equal("Capital of France?", question.Title);
        Assert.Equal(["Paris", "London", "Rome"], question.Choices.Select(choice => choice.Text));
        Assert.Equal(["1", "2", "3"], question.Choices.Select(choice => choice.Value));
        Assert.Equal([question.Choices[0].Id], question.Scoring.CorrectChoiceIds);
        Assert.Equal(2m, question.Scoring.Points);
        Assert.Equal(["geo", "europe"], bankQuestion.Tags);
        Assert.Equal(4, bankQuestion.Difficulty);
        Assert.Equal("Paris is the capital.", question.Scoring.GeneralFeedback);
        Assert.Equal(26, question.Id.Length);
    }

    [Fact]
    public void Read_MatchesTheRightAnswersByChoiceValueToo()
    {
        // Act
        var result = Read(Header + "\r\n,MultipleChoice,Primes?,2|4|5,1|3,,,,");

        // Assert
        var question = Assert.Single(result.Questions).Question;
        Assert.Equal([question.Choices[0].Id, question.Choices[2].Id], question.Scoring.CorrectChoiceIds);
    }

    [Fact]
    public void Read_BuildsRowsChoicesAndPairsForATypeWithRows()
    {
        // Act
        var result = Read(Header + "\r\n,Matching,Match the capitals,France=Paris|Italy=Rome|Spain=Madrid,,,,,");

        // Assert
        var question = Assert.Single(result.Questions).Question;
        Assert.Equal(["France", "Italy", "Spain"], question.Rows.Select(row => row.Text));
        Assert.Equal(["Paris", "Rome", "Madrid"], question.Choices.Select(choice => choice.Text));
        Assert.Equal(question.Choices[1].Id, question.Scoring.Pairs[question.Rows[1].Id]);
    }

    [Fact]
    public void Read_ReadsANumberAnswerAYesNoAnswerAndAcceptedTexts()
    {
        // Act
        var result = Read(Header + "\r\n,Number,Two plus two?,,4,,,,\r\n,Boolean,The sky is blue?,,yes,,,,\r\n,ShortText,Color of grass?,,green|Green,,,,");

        // Assert
        Assert.Empty(result.Errors);
        Assert.Equal(4m, result.Questions[0].Question.Scoring.NumberAnswer);
        Assert.Equal(["true"], result.Questions[1].Question.Scoring.AcceptedAnswers);
        Assert.Equal(["green", "Green"], result.Questions[2].Question.Scoring.AcceptedAnswers);
    }

    [Fact]
    public void Read_UsesAPresetAsItsBaseTypeAndRunsItsInitializer()
    {
        // Act
        var result = Read(Header + "\r\n,Nps,How likely are you to recommend us?,,,,,,");

        // Assert
        var question = Assert.Single(result.Questions).Question;
        Assert.Equal(SurveyControlNames.Rating, question.ControlType);
        Assert.Equal(10, (int)question.Settings["Max"]);
    }

    [Fact]
    public void Read_ReportsEveryProblemOfARowAndSkipsBlankRows()
    {
        // Act
        var result = Read(Header + "\r\n,Acme.Unknown,Text,,,,,,\r\n,,,,,,,,\r\n,SingleChoice,Pick one,A|B,C,-1,,9,\r\n,SingleChoice,,,,,,,");

        // Assert
        Assert.Empty(result.Questions);
        Assert.Contains(result.Errors, error => error.StartsWith("Row 2:", StringComparison.Ordinal) && error.Contains("Acme.Unknown", StringComparison.Ordinal));
        Assert.Equal(3, result.Errors.Count(error => error.StartsWith("Row 4:", StringComparison.Ordinal)));
        Assert.Equal(2, result.Errors.Count(error => error.StartsWith("Row 5:", StringComparison.Ordinal)));
        Assert.DoesNotContain(result.Errors, error => error.StartsWith("Row 3:", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_RefusesAFileWithoutTheTypeAndQuestionColumns()
    {
        // Act
        var result = Read("Name,Text\r\na,b");

        // Assert
        Assert.Empty(result.Questions);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task CsvFormat_ReadsQuotedFields()
    {
        // Arrange
        var format = new CsvQuestionImportFormat(Reader(), RecipeRoundTrip.Localizer<CsvQuestionImportFormat>());
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Header + "\r\n,ShortText,\"Say \"\"hi\"\", please\",,,,,,"));

        // Act
        var result = await format.ReadAsync("questions.csv", stream, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Say \"hi\", please", Assert.Single(result.Questions).Question.Title);
    }

    [Fact]
    public async Task ExcelFormat_ReadsTheFirstSheet()
    {
        // Arrange
        var format = new ExcelQuestionImportFormat(Reader(), RecipeRoundTrip.Localizer<ExcelQuestionImportFormat>());
        using var stream = Workbook(
            ["Type", "Question", "Choices", "Correct"],
            ["SingleChoice", "Pick B", "A|B", "B"]);

        // Act
        var result = await format.ReadAsync("questions.xlsx", stream, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Errors);
        var question = Assert.Single(result.Questions).Question;
        Assert.Equal("Pick B", question.Title);
        Assert.Equal([question.Choices[1].Id], question.Scoring.CorrectChoiceIds);
    }

    [Fact]
    public async Task ExcelFormat_RefusesAFileThatIsNotAWorkbook()
    {
        // Arrange
        var format = new ExcelQuestionImportFormat(Reader(), RecipeRoundTrip.Localizer<ExcelQuestionImportFormat>());
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not a workbook"));

        // Act
        var result = await format.ReadAsync("questions.xlsx", stream, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Questions);
        Assert.Single(result.Errors);
    }

    private static SurveyImportResult Read(string csv)
    {
        var (headers, rows) = CsvReader.Parse(csv);

        return Reader().Read(headers, rows);
    }

    private static SpreadsheetQuestionReader Reader()
        => new(new BanksControlRegistry(), RecipeRoundTrip.Localizer<SpreadsheetQuestionReader>());

    private static MemoryStream Workbook(params string[][] rows)
    {
        var stream = new MemoryStream();

        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart();
            workbook.Workbook = new Workbook();
            var worksheet = workbook.AddNewPart<WorksheetPart>();
            var data = new SheetData();
            uint rowIndex = 1;

            foreach (var values in rows)
            {
                var row = new Row { RowIndex = rowIndex };

                for (var i = 0; i < values.Length; i++)
                {
                    row.Append(new Cell
                    {
                        CellReference = ((char)('A' + i)).ToString() + rowIndex,
                        DataType = CellValues.InlineString,
                        InlineString = new InlineString(new Text(values[i])),
                    });
                }

                data.Append(row);
                rowIndex++;
            }

            worksheet.Worksheet = new Worksheet(data);
            workbook.Workbook.AppendChild(new Sheets(new Sheet { Id = workbook.GetIdOfPart(worksheet), SheetId = 1, Name = "Questions" }));
        }

        stream.Position = 0;

        return stream;
    }
}
