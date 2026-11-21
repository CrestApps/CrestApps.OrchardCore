using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Moodle;
using CrestApps.OrchardCore.Learning.Core.Moodle;

namespace CrestApps.OrchardCore.Tests.Learning.Moodle;

/// <summary>
/// How a Moodle question becomes a question of the site, through the question model the QTI import writes rows from, and
/// which questions are refused with a reason.
/// </summary>
public sealed class MoodleQuestionMapperTests
{
    [Fact]
    public void ASingleAnswerMultipleChoice_BecomesASingleChoice_WithPlainOptionsAndTheirFeedback()
    {
        // Arrange
        var question = Question("multichoice", "<p>Which extinguisher is for <b>electrical</b> fires?</p>", answers:
        [
            Answer("<p>CO<sub>2</sub></p>", 1, "<p>Right: it does not conduct.</p>"),
            Answer("<p>Water &amp; foam</p>", 0, "<p>Water conducts.</p>"),
        ]);
        question.Options["single"] = "1";
        question.Options["shuffleanswers"] = "1";

        // Act
        var mapping = MoodleQuestionMapper.Map(question, "moodle:site:question:55", "course.mbz/questions.xml");

        // Assert
        Assert.Null(mapping.Refusal);
        var item = mapping.Item;
        Assert.Equal("moodle:site:question:55", item.Identifier);
        Assert.Equal("course.mbz/questions.xml", item.Path);
        Assert.Equal(AssessmentsConstants.QuestionTypes.SingleChoice, item.Question.QuestionType);
        Assert.Equal("<p>Which extinguisher is for <b>electrical</b> fires?</p>", item.Question.Prompt);
        Assert.Equal(["CO2", "Water & foam"], item.Question.Options.Select(option => option.Text));
        Assert.Equal([true, false], item.Question.Options.Select(option => option.Correct));
        Assert.Equal(["Right: it does not conduct.", "Water conducts."], item.Question.Options.Select(option => option.Feedback));
        Assert.True(item.Question.Shuffle);
        Assert.Equal(2, item.Question.Points);
    }

    [Fact]
    public void AMultipleAnswerMultipleChoice_BecomesAMultipleChoice_WithPartialCredit()
    {
        // Arrange
        var question = Question("multichoice", "<p>Pick the exits.</p>", answers: [Answer("North", 0.5m), Answer("South", 0.5m), Answer("Lift", -1m)]);
        question.Options["single"] = "0";

        // Act
        var mapping = MoodleQuestionMapper.Map(question, "id", "file");

        // Assert
        Assert.Equal(AssessmentsConstants.QuestionTypes.MultipleChoice, mapping.Item.Question.QuestionType);
        Assert.Equal([true, true, false], mapping.Item.Question.Options.Select(option => option.Correct));
        Assert.True(mapping.Item.Question.PartialCredit);
    }

    [Fact]
    public void ATrueFalse_KeepsItsAnswer()
    {
        // Arrange
        var question = Question("truefalse", "<p>Lifts are safe in a fire.</p>", answers: [Answer("True", 0, id: "a1"), Answer("False", 1, id: "a2")]);
        question.Options["trueanswer"] = "a1";
        question.Options["falseanswer"] = "a2";

        // Act
        var mapping = MoodleQuestionMapper.Map(question, "id", "file");

        // Assert
        Assert.Equal(AssessmentsConstants.QuestionTypes.TrueFalse, mapping.Item.Question.QuestionType);
        Assert.False(mapping.Item.Question.TrueFalseAnswer);
    }

    [Fact]
    public void AShortAnswer_KeepsTheAnswersWorthTheFullMark_AndItsCase()
    {
        // Arrange
        var question = Question("shortanswer", "<p>Name the gas.</p>", answers: [Answer("Oxygen", 1), Answer("O2", 1), Answer("Air", 0.5m)]);
        question.Options["usecase"] = "1";

        // Act
        var mapping = MoodleQuestionMapper.Map(question, "id", "file");

        // Assert
        Assert.Equal(AssessmentsConstants.QuestionTypes.ShortText, mapping.Item.Question.QuestionType);
        Assert.Equal(["Oxygen", "O2"], mapping.Item.Question.AcceptedAnswers);
        Assert.True(mapping.Item.Question.CaseSensitive);
    }

    [Fact]
    public void ANumerical_KeepsItsAnswerAndTolerance()
    {
        // Arrange
        var question = Question("numerical", "<p>How many metres?</p>", answers: [Answer("2.5", 1, id: "n1")]);
        question.Tolerances["n1"] = 0.1m;

        // Act
        var mapping = MoodleQuestionMapper.Map(question, "id", "file");

        // Assert
        Assert.Equal(AssessmentsConstants.QuestionTypes.Numeric, mapping.Item.Question.QuestionType);
        Assert.Equal(2.5m, mapping.Item.Question.NumericAnswer);
        Assert.Equal(0.1m, mapping.Item.Question.Tolerance);
    }

    [Fact]
    public void AnEssay_KeepsTheGraderInformationAsGuidance()
    {
        // Arrange
        var question = Question("essay", "<p>Describe the drill.</p>");
        question.Options["graderinfo"] = "<p>Look for the meeting point.</p>";

        // Act
        var mapping = MoodleQuestionMapper.Map(question, "id", "file");

        // Assert
        Assert.Equal(AssessmentsConstants.QuestionTypes.Essay, mapping.Item.Question.QuestionType);
        Assert.Equal("Look for the meeting point.", mapping.Item.Question.GraderGuidance);
    }

    [Fact]
    public void AMatching_KeepsItsPairs_AndIsRefusedWithDistractors()
    {
        // Arrange
        var pairs = Question("match", "<p>Match them.</p>");
        pairs.Matches.Add(("<p>Fire</p>", "Extinguisher"));
        pairs.Matches.Add(("<p>Smoke</p>", "Alarm"));
        var distractors = Question("match", "<p>Match them.</p>");
        distractors.Matches.Add(("<p>Fire</p>", "Extinguisher"));
        distractors.Matches.Add((string.Empty, "Umbrella"));

        // Act
        var mapped = MoodleQuestionMapper.Map(pairs, "id", "file");
        var refused = MoodleQuestionMapper.Map(distractors, "id", "file");

        // Assert
        Assert.Equal(AssessmentsConstants.QuestionTypes.Matching, mapped.Item.Question.QuestionType);
        Assert.Equal([("Fire", "Extinguisher"), ("Smoke", "Alarm")], mapped.Item.Question.Pairs);
        Assert.Equal(MoodleQuestionRefusals.Distractors, refused.Refusal);
    }

    [Theory]
    [InlineData("ddwtos", AssessmentsConstants.QuestionTypes.DragIntoText)]
    [InlineData("gapselect", AssessmentsConstants.QuestionTypes.GapSelect)]
    public void DragIntoTextAndGapSelect_KeepTheirChoices_WithTheUnusedOnesAsDistractors(string type, string questionType)
    {
        // Arrange
        var question = Question(type, "<p>A fire needs fuel, [[1]] and [[2]].</p>", answers: [Answer("heat", 0), Answer("oxygen", 0), Answer("water", 0)]);
        question.Options["shuffleanswers"] = "1";

        // Act
        var mapping = MoodleQuestionMapper.Map(question, "id", "file");

        // Assert
        Assert.Null(mapping.Refusal);
        Assert.Equal(questionType, mapping.Item.Question.QuestionType);
        Assert.Equal("A fire needs fuel, [[heat]] and [[oxygen]].", mapping.Item.Question.BlankText);
        Assert.Equal(["water"], mapping.Item.Question.Distractors);
        Assert.True(mapping.Item.Question.Shuffle);
    }

    [Fact]
    public void AMultipleChoiceWithNoRightAnswer_BecomesASurveyQuestion_WhereEveryAnswerCounts()
    {
        // Arrange
        var question = Question("multichoice", "<p>How confident are you?</p>", answers: [Answer("Very", 0), Answer("Not much", 0)]);
        question.Options["single"] = "1";

        // Act
        var mapping = MoodleQuestionMapper.Map(question, "id", "file");

        // Assert
        Assert.Null(mapping.Refusal);
        Assert.Equal(AssessmentsConstants.QuestionTypes.Survey, mapping.Item.Question.QuestionType);
        Assert.Equal(["Very", "Not much"], mapping.Item.Question.Options.Select(option => option.Text));
        Assert.False(mapping.Item.Question.PartialCredit);
    }

    [Fact]
    public void ADescription_BecomesADescriptionWorthNothing()
    {
        // Act
        var mapping = MoodleQuestionMapper.Map(Question("description", "<p>Read the next questions carefully.</p>"), "id", "file");

        // Assert
        Assert.Null(mapping.Refusal);
        Assert.Equal(AssessmentsConstants.QuestionTypes.Description, mapping.Item.Question.QuestionType);
        Assert.Equal(0, mapping.Item.Question.Points);
        Assert.Equal("<p>Read the next questions carefully.</p>", mapping.Item.Question.Prompt);
    }

    [Theory]
    [InlineData("multianswer")]
    [InlineData("calculated")]
    [InlineData("ddimageortext")]
    [InlineData("randomsamatch")]
    public void AQuestionTypeTheSiteDoesNotHave_IsRefused_NamingTheType(string type)
    {
        // Act
        var mapping = MoodleQuestionMapper.Map(Question(type, "<p>Text</p>"), "id", "file");

        // Assert
        Assert.Null(mapping.Item.Question);
        Assert.Equal(MoodleQuestionRefusals.UnsupportedType, mapping.Refusal);
        Assert.Equal(type, mapping.Detail);
    }

    [Fact]
    public void AQuestionWithAPicture_OrAnAnswerWithMarkup_IsRefused()
    {
        // Arrange
        var picture = Question("truefalse", "<p>Is this safe? <img src=\"@@PLUGINFILE@@/exit.png\"></p>", answers: [Answer("True", 1, id: "t"), Answer("False", 0, id: "f")]);
        picture.Options["trueanswer"] = "t";
        var markup = Question("multichoice", "<p>Pick one.</p>", answers: [Answer("<img src=\"@@PLUGINFILE@@/a.png\">", 1), Answer("B", 0)]);
        markup.Options["single"] = "1";

        // Act
        var pictureMapping = MoodleQuestionMapper.Map(picture, "id", "file");
        var markupMapping = MoodleQuestionMapper.Map(markup, "id", "file");

        // Assert
        Assert.Equal(MoodleQuestionRefusals.Picture, pictureMapping.Refusal);
        Assert.Equal(MoodleQuestionRefusals.MarkupInAnswer, markupMapping.Refusal);
    }

    private static MoodleQuestion Question(string type, string text, IList<MoodleAnswer> answers = null)
        => new()
        {
            Id = "1",
            BankEntryId = "55",
            Name = "Question " + type,
            Type = type,
            Text = text,
            DefaultMark = 2,
            Answers = answers ?? [],
        };

    private static MoodleAnswer Answer(string text, decimal fraction, string feedback = null, string id = null)
        => new() { Id = id ?? Guid.NewGuid().ToString("n"), Text = text, Fraction = fraction, Feedback = feedback };
}
