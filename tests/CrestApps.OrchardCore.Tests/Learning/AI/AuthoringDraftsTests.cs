using CrestApps.OrchardCore.Learning.AI.Services;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.AI;

/// <summary>
/// What the authoring assistant does with a model's answer: it reads only usable JSON within limits, leaves out what the
/// question types cannot hold, and writes the text into drafts encoded, never as markup.
/// </summary>
public sealed class AuthoringDraftsTests
{
    [Fact]
    public void Outline_IsReadFromAnAnswerWrappedInACodeFence()
    {
        var answer = """
            Here is the outline:
            ```json
            {"title": "Ladder safety", "summary": "Use ladders safely.", "sections": [
              {"title": "Basics", "lessons": [{"title": "Choosing a ladder", "summary": "Pick the right one."}, {"title": "Inspecting", "summary": "Check before use."}]},
              {"title": "Empty", "lessons": []}
            ]}
            ```
            """;

        var outline = AuthoringDraftParser.ParseOutline(answer);

        Assert.NotNull(outline);
        Assert.Equal("Ladder safety", outline.Title);
        var section = Assert.Single(outline.Sections);
        Assert.Equal(["Choosing a ladder", "Inspecting"], section.Lessons.Select(lesson => lesson.Title));
    }

    [Theory]
    [InlineData("")]
    [InlineData("I cannot help with that.")]
    [InlineData("{\"title\": \"No sections\"}")]
    [InlineData("{not json}")]
    public void Outline_WithNothingUsable_IsNull(string answer)
    {
        Assert.Null(AuthoringDraftParser.ParseOutline(answer));
    }

    [Fact]
    public void Questions_KeepOnlyWhatTheQuestionTypesCanHold()
    {
        var answer = """
            {"questions": [
              {"type": "SingleChoice", "prompt": "One right", "options": [{"text": "A", "correct": true}, {"text": "B", "correct": false}]},
              {"type": "SingleChoice", "prompt": "Two right", "options": [{"text": "A", "correct": true}, {"text": "B", "correct": true}]},
              {"type": "MultipleChoice", "prompt": "Many right", "options": [{"text": "A", "correct": true}, {"text": "B", "correct": true}, {"text": "C"}]},
              {"type": "TrueFalse", "prompt": "Ladders lean at 75 degrees", "answer": true},
              {"type": "ShortText", "prompt": "No answers", "acceptedAnswers": []},
              {"type": "ShortText", "prompt": "Name the rule", "acceptedAnswers": ["one in four"]},
              {"type": "Essay", "prompt": "Explain", "feedback": "Mentions the angle", "points": 5},
              {"type": "Hotspot", "prompt": "Unknown type"}
            ]}
            """;

        var questions = AuthoringDraftParser.ParseQuestions(answer);

        Assert.Equal(["One right", "Many right", "Ladders lean at 75 degrees", "Name the rule", "Explain"], questions.Select(question => question.Prompt));
        Assert.True(questions[2].Answer);
        Assert.Equal(5m, questions[4].Points);
    }

    [Fact]
    public void Questions_AreCappedAtTheLimit()
    {
        var items = string.Join(",", Enumerable.Range(1, 30).Select(index => $$"""{"type": "TrueFalse", "prompt": "Q{{index}}", "answer": false}"""));

        Assert.Equal(AuthoringDraftParser.MaxQuestions, AuthoringDraftParser.ParseQuestions("{\"questions\": [" + items + "]}").Count);
    }

    [Fact]
    public void Rubric_KeepsCriteriaWithTwoLevelsOrMore()
    {
        var rubric = AuthoringDraftParser.ParseRubric("""
            {"title": "Report", "criteria": [
              {"title": "Clarity", "levels": [{"title": "Weak", "points": 0}, {"title": "Strong", "points": 4}]},
              {"title": "One level", "levels": [{"title": "Only", "points": 1}]},
              {"title": "Bad points", "levels": [{"title": "A", "points": -1}, {"title": "B", "points": "x"}]}
            ]}
            """);

        Assert.NotNull(rubric);
        var criterion = Assert.Single(rubric.Criteria);
        Assert.Equal("Clarity", criterion.Title);
    }

    [Fact]
    public void Translation_NeedsATitleAndABody()
    {
        Assert.Equal("Sécurité", AuthoringDraftParser.ParseTranslation("""{"title": "Sécurité", "body": "<p>Texte</p>"}""")?.Title);
        Assert.Null(AuthoringDraftParser.ParseTranslation("""{"title": "Sécurité"}"""));
    }

    [Fact]
    public void Questions_AreWrittenIntoTheirTypesWithTheTextEncoded()
    {
        var choice = new DraftQuestion { Type = "SingleChoice", Prompt = "Pick <script>x</script>", Feedback = "Because." };
        choice.Options.Add(new DraftOption { Text = "A", Correct = true });
        choice.Options.Add(new DraftOption { Text = "B" });
        var item = new ContentItem { ContentType = AuthoringDraftBuilder.QuestionContentType(choice.Type) };

        AuthoringDraftBuilder.ApplyQuestion(item, choice);

        Assert.Equal(AssessmentsConstants.ContentTypes.SingleChoiceQuestion, item.ContentType);
        var question = item.Get<QuestionPart>(nameof(QuestionPart));
        Assert.Equal("<p>Pick &lt;script&gt;x&lt;/script&gt;</p>", question.Prompt);
        Assert.Equal("Because.", question.GeneralFeedback);
        var options = item.Get<ChoiceQuestionPart>(nameof(ChoiceQuestionPart)).Options;
        Assert.Equal(["A", "B"], options.Select(option => option.Text));
        Assert.True(options[0].Correct);
        Assert.Equal(2, options.Select(option => option.Id).Distinct().Count());
    }

    [Fact]
    public void Essay_GetsTheFeedbackAsGraderGuidance()
    {
        var essay = new DraftQuestion { Type = "Essay", Prompt = "Explain", Feedback = "Mentions the angle", Points = 5 };
        var item = new ContentItem { ContentType = AuthoringDraftBuilder.QuestionContentType(essay.Type) };

        AuthoringDraftBuilder.ApplyQuestion(item, essay);

        Assert.Equal(AssessmentsConstants.ContentTypes.EssayQuestion, item.ContentType);
        Assert.Equal("Mentions the angle", item.Get<EssayQuestionPart>(nameof(EssayQuestionPart)).GraderGuidance);
        Assert.Null(item.Get<QuestionPart>(nameof(QuestionPart)).GeneralFeedback);
    }

    [Fact]
    public void Rubric_IsWrittenWithUniqueCriterionAndLevelIds()
    {
        var draft = new DraftRubric { Title = "Report" };
        var clarity = new DraftCriterion { Title = "Clarity" };
        clarity.Levels.Add(new DraftLevel { Title = "Weak", Points = 0 });
        clarity.Levels.Add(new DraftLevel { Title = "Strong", Points = 4 });
        var depth = new DraftCriterion { Title = "Depth" };
        depth.Levels.Add(new DraftLevel { Title = "Thin", Points = 0 });
        depth.Levels.Add(new DraftLevel { Title = "Deep", Points = 4 });
        draft.Criteria.Add(clarity);
        draft.Criteria.Add(depth);
        var item = new ContentItem { ContentType = "Rubric" };

        AuthoringDraftBuilder.ApplyRubric(item, draft);

        var criteria = item.Get<RubricPart>(nameof(RubricPart)).Criteria;
        Assert.Equal(["Clarity", "Depth"], criteria.Select(criterion => criterion.Title));
        Assert.Equal(4, criteria.SelectMany(criterion => criterion.Levels).Select(level => level.Id).Distinct().Count());
        Assert.Equal("Report", item.DisplayText);
    }
}
