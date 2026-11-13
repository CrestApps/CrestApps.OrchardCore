using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Scoring;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// Each question type copies what it shows and what scores it into the attempt item, never the key into what is shown,
/// and scores a response from the item alone.
/// </summary>
public sealed class QuestionScorerTests
{
    [Theory]
    [InlineData(new[] { "b" }, 2)]
    [InlineData(new[] { "a" }, 0)]
    [InlineData(new string[0], 0)]
    [InlineData(new[] { "a", "b" }, 0)]
    public void SingleChoice_ScoresOnlyTheOneRightOption(string[] chosen, double expected)
    {
        var item = Item(new SingleChoiceScorer(), Choice(multiple: false, ("a", false), ("b", true), ("c", false)), points: 2);
        item.Response = new AttemptResponse { ChoiceIds = chosen };

        Assert.Equal((decimal)expected, new SingleChoiceScorer().Score(item));
    }

    [Theory]
    [InlineData(new[] { "a", "c" }, false, 3)]
    [InlineData(new[] { "a" }, false, 0)]
    [InlineData(new[] { "a" }, true, 1.5)]
    [InlineData(new[] { "a", "b" }, true, 0)]
    [InlineData(new[] { "a", "c", "b" }, true, 1.5)]
    [InlineData(new[] { "a", "c", "b" }, false, 0)]
    public void MultipleChoice_IsAllOrNothing_OrRightLessWrongWithPartialCredit(string[] chosen, bool partial, double expected)
    {
        var scorer = new MultipleChoiceScorer();
        var question = Choice(multiple: true, ("a", true), ("b", false), ("c", true), ("d", false));
        question.Alter<ChoiceQuestionPart>(part => part.PartialCredit = partial);
        var item = Item(scorer, question, points: 3);
        item.Response = new AttemptResponse { ChoiceIds = chosen };

        Assert.Equal((decimal)expected, scorer.Score(item));
    }

    [Theory]
    [InlineData("true", 1)]
    [InlineData("false", 0)]
    public void TrueFalse_ScoresTheRightValue(string chosen, double expected)
    {
        var scorer = new TrueFalseScorer();
        var question = Question(AssessmentsConstants.ContentTypes.TrueFalseQuestion);
        question.Alter<TrueFalseQuestionPart>(part => part.Answer = true);
        var item = Item(scorer, question, points: 1);
        item.Response = new AttemptResponse { ChoiceIds = [chosen] };

        Assert.Equal((decimal)expected, scorer.Score(item));
        Assert.Equal(["true", "false"], item.Choices.Select(choice => choice.Id));
    }

    [Theory]
    [InlineData("Paris", false, 1)]
    [InlineData("  paris  ", false, 1)]
    [InlineData("paris", true, 0)]
    [InlineData("City of Light", false, 1)]
    [InlineData("city  of light", false, 1)]
    [InlineData("Lyon", false, 0)]
    public void ShortText_MatchesAnyAcceptedAnswer_TrimmedAndWithCaseAsSet(string answer, bool caseSensitive, double expected)
    {
        var scorer = new ShortTextScorer();
        var question = Question(AssessmentsConstants.ContentTypes.ShortTextQuestion);
        question.Alter<ShortTextQuestionPart>(part =>
        {
            part.AcceptedAnswers = ["Paris", "City of Light"];
            part.CaseSensitive = caseSensitive;
        });
        var item = Item(scorer, question, points: 1);
        item.Response = new AttemptResponse { Texts = [answer] };

        Assert.Equal((decimal)expected, scorer.Score(item));
    }

    [Theory]
    [InlineData(9.81, 1)]
    [InlineData(9.75, 1)]
    [InlineData(9.87, 1)]
    [InlineData(9.7, 0)]
    public void Numeric_AcceptsAnAnswerWithinTheTolerance(double answer, double expected)
    {
        var scorer = new NumericScorer();
        var question = Question(AssessmentsConstants.ContentTypes.NumericQuestion);
        question.Alter<NumericQuestionPart>(part =>
        {
            part.Answer = 9.81m;
            part.Tolerance = 0.06m;
        });
        var item = Item(scorer, question, points: 1);
        item.Response = new AttemptResponse { Number = (decimal)answer };

        Assert.Equal((decimal)expected, scorer.Score(item));
    }

    [Fact]
    public void Matching_GivesEachRightMatchItsShare_AndHidesThePairsInWhatIsShown()
    {
        var scorer = new MatchingScorer();
        var question = Question(AssessmentsConstants.ContentTypes.MatchingQuestion);
        question.Alter<MatchingQuestionPart>(part => part.Pairs =
        [
            new MatchingPair { Id = "1", Prompt = "H2O", Answer = "Water" },
            new MatchingPair { Id = "2", Prompt = "NaCl", Answer = "Salt" },
        ]);
        var item = Item(scorer, question, points: 2);

        var waterId = item.Answers.Single(answer => answer.Text == "Water").Id;
        var saltId = item.Answers.Single(answer => answer.Text == "Salt").Id;
        item.Response = new AttemptResponse { Pairs = new Dictionary<string, string> { ["1"] = waterId, ["2"] = waterId } };

        Assert.Equal(1m, scorer.Score(item));
        Assert.NotEqual("1", waterId);
        Assert.DoesNotContain(item.Choices, choice => choice.Text == "Water");

        item.Response = new AttemptResponse { Pairs = new Dictionary<string, string> { ["1"] = waterId, ["2"] = saltId } };
        Assert.Equal(2m, scorer.Score(item));
    }

    [Theory]
    [InlineData(new[] { "1", "2", "3" }, false, 3)]
    [InlineData(new[] { "1", "3", "2" }, false, 0)]
    [InlineData(new[] { "1", "3", "2" }, true, 1)]
    public void Ordering_ScoresTheRightOrder(string[] order, bool partial, double expected)
    {
        var scorer = new OrderingScorer();
        var question = Question(AssessmentsConstants.ContentTypes.OrderingQuestion);
        question.Alter<OrderingQuestionPart>(part =>
        {
            part.Items = [new ChoiceOption { Id = "1", Text = "First" }, new ChoiceOption { Id = "2", Text = "Second" }, new ChoiceOption { Id = "3", Text = "Third" }];
            part.PartialCredit = partial;
        });
        var item = Item(scorer, question, points: 3);
        item.Response = new AttemptResponse { ChoiceIds = order };

        Assert.Equal((decimal)expected, scorer.Score(item));
    }

    [Fact]
    public void FillInBlanks_SplitsTheText_AndGivesEachBlankItsShare()
    {
        var scorer = new FillInBlanksScorer();
        var question = Question(AssessmentsConstants.ContentTypes.FillInBlanksQuestion);
        question.Alter<FillInBlanksQuestionPart>(part => part.Text = "The sky is [[blue]] and grass is [[green|verdant]].");
        var item = Item(scorer, question, points: 2);

        Assert.Equal(2, item.BlankCount);
        Assert.Equal(["The sky is ", " and grass is ", "."], item.Segments);
        Assert.DoesNotContain(item.Segments, segment => segment.Contains("blue", StringComparison.Ordinal));

        item.Response = new AttemptResponse { Texts = ["Blue", "red"] };
        Assert.Equal(1m, scorer.Score(item));

        item.Response = new AttemptResponse { Texts = ["blue", "Verdant"] };
        Assert.Equal(2m, scorer.Score(item));
    }

    [Fact]
    public void Essay_LeavesAnAnswerToTheGrader_AndScoresAnEmptyOneZero()
    {
        var scorer = new EssayScorer();
        var question = Question(AssessmentsConstants.ContentTypes.EssayQuestion);
        question.Alter<EssayQuestionPart>(part =>
        {
            part.MaxWords = 300;
            part.GraderGuidance = "Look for three causes.";
        });
        var item = Item(scorer, question, points: 5);

        item.Response = new AttemptResponse { Texts = ["Because of the heat and the wind."] };
        Assert.Null(scorer.Score(item));

        item.Response = new AttemptResponse { Texts = ["   "] };
        Assert.Equal(0m, scorer.Score(item));
        Assert.Equal(300, item.MaxWords);
        Assert.DoesNotContain("three causes", item.Prompt ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Snapshot_ShowsNoKey_AndKeepsOptionOrderWhenNotShuffled()
    {
        var scorer = new SingleChoiceScorer();
        var question = Choice(multiple: false, ("a", false), ("b", true), ("c", false));
        question.Alter<ChoiceQuestionPart>(part => part.ShuffleOptions = false);

        var item = Item(scorer, question, points: 1);

        Assert.Equal(["a", "b", "c"], item.Choices.Select(choice => choice.Id));
        Assert.Equal(["b"], item.Key.ChoiceIds);
        Assert.Equal("q1", item.QuestionId);
        Assert.Equal("q1-v3", item.QuestionVersionId);
        Assert.Equal("Which one?", item.Prompt);
        Assert.Equal(1m, item.Points);
    }

    private static AttemptItem Item(IQuestionScorer scorer, ContentItem question, decimal points)
    {
        question.Alter<QuestionPart>(part => part.Points = points);
        var item = new AttemptItem
        {
            QuestionId = question.ContentItemId,
            QuestionVersionId = question.ContentItemVersionId,
            QuestionType = scorer.QuestionType,
            Prompt = question.Get<QuestionPart>(nameof(QuestionPart)).Prompt,
            Points = points,
        };

        scorer.Snapshot(question, item, new Random(7));

        return item;
    }

    private static ContentItem Question(string contentType)
    {
        var question = new ContentItem
        {
            ContentItemId = "q1",
            ContentItemVersionId = "q1-v3",
            ContentType = contentType,
            DisplayText = "Question",
        };

        question.Alter<QuestionPart>(part => part.Prompt = "Which one?");

        return question;
    }

    private static ContentItem Choice(bool multiple, params (string Id, bool Correct)[] options)
    {
        var question = Question(multiple ? AssessmentsConstants.ContentTypes.MultipleChoiceQuestion : AssessmentsConstants.ContentTypes.SingleChoiceQuestion);

        question.Alter<ChoiceQuestionPart>(part => part.Options = options
            .Select(option => new ChoiceOption { Id = option.Id, Text = "Option " + option.Id, Correct = option.Correct, Feedback = "Because " + option.Id })
            .ToList());

        return question;
    }
}
