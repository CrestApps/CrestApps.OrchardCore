using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Scoring;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// The question types a Moodle quiz needs beside the first nine: survey questions every answer counts in, drag into
/// text and gapselect (blanks filled from a list of choices with distractors), and a description between questions.
/// </summary>
public sealed class MoodleQuestionTypesTests
{
    [Fact]
    public void Survey_IsUngradedAtZeroPoints_AndAnyAnswerEarnsThePointsWhenItHasSome()
    {
        // Arrange
        var scorer = new SurveyScorer();
        var question = Question(AssessmentsConstants.ContentTypes.SurveyQuestion);
        question.Alter<SurveyQuestionPart>(part =>
        {
            part.Options = [new ChoiceOption { Id = "yes", Text = "Yes" }, new ChoiceOption { Id = "no", Text = "No" }];
            part.Multiple = true;
        });
        var ungraded = Item(scorer, question, points: 0);
        var graded = Item(scorer, question, points: 2);

        // Act
        ungraded.Response = new AttemptResponse { ChoiceIds = ["no"] };
        graded.Response = new AttemptResponse { ChoiceIds = ["no"] };
        var gradedEmpty = Item(scorer, question, points: 2);
        gradedEmpty.Response = new AttemptResponse();

        // Assert
        Assert.Equal(0m, scorer.Score(ungraded));
        Assert.Equal(2m, scorer.Score(graded));
        Assert.Equal(0m, scorer.Score(gradedEmpty));
        Assert.True(graded.Multiple);
        Assert.Equal(["yes", "no"], graded.Choices.Select(choice => choice.Id));
        Assert.Empty(graded.Key.ChoiceIds);
    }

    [Theory]
    [InlineData(AssessmentsConstants.QuestionTypes.DragIntoText)]
    [InlineData(AssessmentsConstants.QuestionTypes.GapSelect)]
    public void ChoiceBlanks_OfferTheAnswersAndTheDistractors_HideWhichIsRight_AndGiveEachBlankItsShare(string questionType)
    {
        // Arrange
        IQuestionScorer scorer = questionType == AssessmentsConstants.QuestionTypes.DragIntoText ? new DragIntoTextScorer() : new GapSelectScorer();
        var question = Question(questionType == AssessmentsConstants.QuestionTypes.DragIntoText ? AssessmentsConstants.ContentTypes.DragIntoTextQuestion : AssessmentsConstants.ContentTypes.GapSelectQuestion);
        question.Alter<ChoiceBlanksQuestionPart>(part =>
        {
            part.Text = "Fire needs [[heat]], [[fuel]] and [[oxygen]].";
            part.Distractors = ["water", "sand", "fuel"];
        });

        // Act
        var item = Item(scorer, question, points: 3);
        string Id(string text) => item.Choices.Single(choice => choice.Text == text).Id;

        // Assert
        Assert.Equal(3, item.BlankCount);
        Assert.Equal(["Fire needs ", ", ", " and ", "."], item.Segments);
        Assert.Equal(["fuel", "heat", "oxygen", "sand", "water"], item.Choices.Select(choice => choice.Text).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(item.Choices, choice => choice.Id is "heat" or "fuel" or "oxygen");
        Assert.Equal([Id("heat"), Id("fuel"), Id("oxygen")], item.Key.ChoiceIds);

        item.Response = new AttemptResponse { ChoiceIds = [Id("heat"), Id("water"), string.Empty] };
        Assert.Equal(1m, scorer.Score(item));

        item.Response = new AttemptResponse { ChoiceIds = [Id("heat"), Id("fuel"), Id("oxygen")] };
        Assert.Equal(3m, scorer.Score(item));
    }

    [Fact]
    public void ChoiceBlanks_KeepTheWrittenOrderWhenNotShuffled()
    {
        // Arrange
        var scorer = new GapSelectScorer();
        var question = Question(AssessmentsConstants.ContentTypes.GapSelectQuestion);
        question.Alter<ChoiceBlanksQuestionPart>(part =>
        {
            part.Text = "[[red]] and [[blue]]";
            part.Distractors = ["green"];
            part.ShuffleChoices = false;
        });

        // Act
        var item = Item(scorer, question, points: 1);

        // Assert
        Assert.Equal(["red", "blue", "green"], item.Choices.Select(choice => choice.Text));
    }

    [Fact]
    public void Description_IsWorthNothing_AndNeedsNoAnswer()
    {
        // Arrange
        var scorer = new DescriptionScorer();
        var question = Question(AssessmentsConstants.ContentTypes.DescriptionQuestion);

        // Act
        var item = Item(scorer, question, points: 5);

        // Assert
        Assert.Equal(0m, item.Points);
        Assert.Equal(0m, scorer.Score(item));
    }

    [Fact]
    public void FormReader_ReadsSurveyChoicesAndChoiceBlanks_KeepingOnlyOfferedChoices()
    {
        // Arrange
        var survey = new AttemptItem { Index = 0, QuestionType = AssessmentsConstants.QuestionTypes.Survey, Choices = [new() { Id = "yes" }, new() { Id = "no" }] };
        var drag = new AttemptItem { Index = 1, QuestionType = AssessmentsConstants.QuestionTypes.DragIntoText, BlankCount = 2, Choices = [new() { Id = "c0" }, new() { Id = "c1" }] };
        var gaps = new AttemptItem { Index = 2, QuestionType = AssessmentsConstants.QuestionTypes.GapSelect, BlankCount = 1, Choices = [new() { Id = "c0" }] };
        var description = new AttemptItem { Index = 3, QuestionType = AssessmentsConstants.QuestionTypes.Description };
        var form = Form(("a0", ["no", "forged"]), ("a1_b0", ["c1"]), ("a1_b1", ["forged"]), ("a2_b0", ["c0"]), ("a3", ["anything"]));

        // Act and assert
        Assert.Equal(["no"], AttemptFormReader.Read(form, survey).ChoiceIds);
        Assert.Equal(["c1", string.Empty], AttemptFormReader.Read(form, drag).ChoiceIds);
        Assert.Equal(["c0"], AttemptFormReader.Read(form, gaps).ChoiceIds);
        Assert.False(AttemptFormReader.HasAnswer(form, description));
    }

    private static AttemptItem Item(IQuestionScorer scorer, ContentItem question, decimal points)
    {
        question.Alter<QuestionPart>(part => part.Points = points);
        var item = new AttemptItem
        {
            QuestionId = question.ContentItemId,
            QuestionType = scorer.QuestionType,
            Points = points,
        };

        scorer.Snapshot(question, item, new Random(7));

        return item;
    }

    private static ContentItem Question(string contentType)
    {
        var question = new ContentItem { ContentItemId = "q1", ContentItemVersionId = "q1-v1", ContentType = contentType, DisplayText = "Question" };
        question.Alter<QuestionPart>(part => part.Prompt = "<p>Read this.</p>");

        return question;
    }

    private static FormCollection Form(params (string Key, string[] Values)[] fields)
        => new(fields.ToDictionary(field => field.Key, field => new StringValues(field.Values)));
}
