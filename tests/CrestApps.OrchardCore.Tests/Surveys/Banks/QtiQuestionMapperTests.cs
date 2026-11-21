using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Core.Qti;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.Banks;

/// <summary>
/// QTI questions mapped to Surveys question types.
/// </summary>
public sealed class QtiQuestionMapperTests
{
    [Theory]
    [InlineData(QtiQuestionTypes.SingleChoice, SurveyControlNames.SingleChoice)]
    [InlineData(QtiQuestionTypes.MultipleChoice, SurveyControlNames.MultipleChoice)]
    [InlineData(QtiQuestionTypes.TrueFalse, SurveyControlNames.Boolean)]
    [InlineData(QtiQuestionTypes.ShortText, SurveyControlNames.ShortText)]
    [InlineData(QtiQuestionTypes.Numeric, SurveyControlNames.Number)]
    [InlineData(QtiQuestionTypes.Essay, SurveyControlNames.LongText)]
    [InlineData(QtiQuestionTypes.Matching, SurveyControlNames.Matching)]
    [InlineData(QtiQuestionTypes.Ordering, SurveyControlNames.Ranking)]
    [InlineData(QtiQuestionTypes.FillInBlanks, SurveyControlNames.FillInBlanks)]
    [InlineData(QtiQuestionTypes.DragIntoText, SurveyControlNames.ChoiceBlanks)]
    [InlineData(QtiQuestionTypes.GapSelect, SurveyControlNames.ChoiceBlanks)]
    [InlineData(QtiQuestionTypes.Description, SurveyControlNames.TextBlock)]
    public void Map_TurnsEveryQtiKindIntoASurveysType(string qtiType, string controlType)
    {
        // Act
        var mapped = QtiQuestionMapper.Map(Item(new QtiQuestion { QuestionType = qtiType, Prompt = "<p>Question &amp; text</p>" }));

        // Assert
        Assert.Equal(controlType, mapped.Question.ControlType);
        Assert.Equal("Question & text", mapped.Question.Title);
        Assert.Equal("qti:item-1", mapped.SourceIdentifier);
    }

    [Fact]
    public void Map_KeepsTheChoicesTheRightOnesAndTheirFeedback()
    {
        // Arrange
        var qti = new QtiQuestion
        {
            QuestionType = QtiQuestionTypes.MultipleChoice,
            Prompt = "Primes?",
            Points = 3,
            Shuffle = true,
            PartialCredit = true,
            GeneralFeedback = "Primes have two divisors.",
            Options =
            [
                new QtiOption { Identifier = "A", Text = "2", Correct = true, Feedback = "Yes" },
                new QtiOption { Identifier = "B", Text = "4" },
                new QtiOption { Identifier = "C", Text = "5", Correct = true },
            ],
        };

        // Act
        var question = QtiQuestionMapper.Map(Item(qti)).Question;

        // Assert
        Assert.Equal(["A", "B", "C"], question.Choices.Select(choice => choice.Value));
        Assert.Equal([question.Choices[0].Id, question.Choices[2].Id], question.Scoring.CorrectChoiceIds);
        Assert.Equal("Yes", question.Scoring.ChoiceFeedback[question.Choices[0].Id]);
        Assert.True(question.ShuffleChoices);
        Assert.True(question.Scoring.PartialCredit);
        Assert.Equal(3m, question.Scoring.Points);
        Assert.Equal("Primes have two divisors.", question.Scoring.GeneralFeedback);
        Assert.Null(question.Description);
    }

    [Fact]
    public void Map_BuildsRowsChoicesAndPairsOfAMatchingQuestionWithDistractors()
    {
        // Arrange
        var qti = new QtiQuestion
        {
            QuestionType = QtiQuestionTypes.Matching,
            Prompt = "Match",
            Pairs = [("France", "Paris"), ("Italy", "Rome")],
            Distractors = ["Berlin"],
        };

        // Act
        var question = QtiQuestionMapper.Map(Item(qti)).Question;

        // Assert
        Assert.Equal(["France", "Italy"], question.Rows.Select(row => row.Text));
        Assert.Equal(["Paris", "Rome", "Berlin"], question.Choices.Select(choice => choice.Text));
        Assert.Equal(question.Choices[1].Id, question.Scoring.Pairs[question.Rows[1].Id]);
    }

    [Fact]
    public void Map_WritesTheBlankTextDistractorsAndDisplayOfAGapSelectQuestion()
    {
        // Arrange
        var qti = new QtiQuestion
        {
            QuestionType = QtiQuestionTypes.GapSelect,
            Prompt = "Fill",
            BlankText = "The [[sky]] is blue.",
            Distractors = ["sea"],
        };

        // Act
        var question = QtiQuestionMapper.Map(Item(qti)).Question;

        // Assert
        Assert.Equal("The [[sky]] is blue.", (string)question.Settings["Text"]);
        Assert.Equal("GapSelect", (string)question.Settings["Display"]);
        Assert.Equal(["sea"], ((JsonArray)question.Settings["Distractors"]).Select(node => (string)node));
    }

    [Fact]
    public void Map_KeepsTheKeyOfNumberTextAndTrueOrFalseQuestions()
    {
        // Act
        var number = QtiQuestionMapper.Map(Item(new QtiQuestion { QuestionType = QtiQuestionTypes.Numeric, Prompt = "n", NumericAnswer = 3.5m, Tolerance = 0.1m })).Question;
        var text = QtiQuestionMapper.Map(Item(new QtiQuestion { QuestionType = QtiQuestionTypes.ShortText, Prompt = "t", AcceptedAnswers = ["x", "y"], CaseSensitive = true })).Question;
        var boolean = QtiQuestionMapper.Map(Item(new QtiQuestion { QuestionType = QtiQuestionTypes.TrueFalse, Prompt = "b", TrueFalseAnswer = false })).Question;

        // Assert
        Assert.Equal(3.5m, number.Scoring.NumberAnswer);
        Assert.Equal(0.1m, number.Scoring.Tolerance);
        Assert.Equal(["x", "y"], text.Scoring.AcceptedAnswers);
        Assert.True(text.Scoring.CaseSensitive);
        Assert.Equal(["false"], boolean.Scoring.AcceptedAnswers);
        Assert.False((bool)boolean.Scoring.Key["answer"]);
    }

    [Fact]
    public void Map_KeepsTheChoiceOrderOfAnOrderingQuestionAsItsKey()
    {
        // Act
        var question = QtiQuestionMapper.Map(Item(new QtiQuestion
        {
            QuestionType = QtiQuestionTypes.Ordering,
            Prompt = "Order",
            Options = [new QtiOption { Identifier = "1", Text = "First" }, new QtiOption { Identifier = "2", Text = "Second" }],
        })).Question;

        // Assert
        Assert.Equal(["First", "Second"], question.Choices.Select(choice => choice.Text));
        Assert.True(question.ShuffleChoices);
        Assert.Empty(question.Scoring.CorrectChoiceIds);
    }

    [Fact]
    public void Map_ReturnsNullForARefusedItem()
    {
        // Act
        var mapped = QtiQuestionMapper.Map(new QtiItem { Identifier = "x", Refusal = QtiRefusals.Adaptive, Question = new QtiQuestion { QuestionType = QtiQuestionTypes.SingleChoice } });

        // Assert
        Assert.Null(mapped);
    }

    [Fact]
    public void Map_MapsTheItemsTheReaderReadsFromAPackage()
    {
        // Arrange
        using var package = BanksQtiSamples.Package(
            BanksQtiSamples.Manifest30(("a", "items/a.xml")),
            [("items/a.xml", BanksQtiSamples.Choice30())],
            ("items/images/sign.png", BanksQtiSamples.Png));

        // Act
        var result = QtiReader.Read(package, "package.zip");
        var mapped = result.Items.Where(item => item.Imported).Select(QtiQuestionMapper.Map).ToList();

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotEmpty(mapped);
        Assert.All(mapped, question => Assert.Equal(SurveyControlNames.SingleChoice, question.Question.ControlType));
    }

    private static QtiItem Item(QtiQuestion question)
        => new() { Identifier = "item-1", Title = "Item", Question = question };
}
