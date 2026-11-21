using CrestApps.OrchardCore.Learning.Assessments.Core.Moodle;
using CrestApps.OrchardCore.Learning.Core.Moodle;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Moodle;

/// <summary>
/// How the Moodle settings the import used to report become settings of the site: quiz pages, feedback and review
/// options, SCORM windows, attempts and grading, restrictions, assignments, feedback surveys and choices, completion by
/// role, and the pictures of the backup in HTML.
/// </summary>
public sealed class MoodleActivityMappingTests
{
    [Fact]
    public void AQuiz_KeepsItsQuestionsPerPage_ItsImmediateFeedback_AndItsReviewOptionsPerMoment()
    {
        // Arrange: marks during and right after, right answers and feedback only after the quiz closes.
        var quiz = Activity("quiz", new()
        {
            ["questionsperpage"] = "3",
            ["preferredbehaviour"] = "interactive",
            ["reviewattempt"] = "69648",
            ["reviewmarks"] = "69632",
            ["reviewrightanswer"] = "16",
            ["reviewspecificfeedback"] = "16",
            ["reviewgeneralfeedback"] = "0",
        });

        // Act
        var perPage = MoodleQuizSettings.QuestionsPerPage(quiz);
        var feedback = MoodleQuizSettings.FeedbackMode(quiz);
        var review = MoodleQuizSettings.ReviewOptions(quiz);

        // Assert
        Assert.Equal(3, perPage);
        Assert.Equal(FeedbackMode.Immediate, feedback);
        Assert.Equal(ReviewShows.Responses | ReviewShows.Score, review.During);
        Assert.Equal(ReviewShows.Responses | ReviewShows.Score, review.ImmediatelyAfter);
        Assert.Equal(ReviewShows.None, review.LaterWhileOpen);
        Assert.Equal(ReviewShows.Responses | ReviewShows.RightAnswers | ReviewShows.Feedback, review.AfterClose);
    }

    [Theory]
    [InlineData("deferredfeedback", FeedbackMode.Deferred)]
    [InlineData("immediatefeedback", FeedbackMode.Immediate)]
    [InlineData("immediatecbm", FeedbackMode.Immediate)]
    [InlineData(null, FeedbackMode.Deferred)]
    public void AQuizBehaviour_MapsToAFeedbackMode(string behaviour, FeedbackMode expected)
        => Assert.Equal(expected, MoodleQuizSettings.FeedbackMode(Activity("quiz", new() { ["preferredbehaviour"] = behaviour })));

    [Fact]
    public void AScormPackage_KeepsItsNewWindow_ItsAttempts_AndWhichAttemptGrades()
    {
        // Arrange
        var scorm = Activity("scorm", new() { ["popup"] = "1", ["maxattempt"] = "3", ["whatgrade"] = "1" });
        var unlimited = Activity("scorm", new() { ["popup"] = "0", ["maxattempt"] = "0", ["whatgrade"] = "3" });

        // Assert
        Assert.True(MoodleScormSettings.OpensInNewWindow(scorm));
        Assert.Equal(3, MoodleScormSettings.MaxAttempts(scorm));
        Assert.Equal(GradingMethod.Average, MoodleScormSettings.Grading(scorm));
        Assert.False(MoodleScormSettings.OpensInNewWindow(unlimited));
        Assert.Null(MoodleScormSettings.MaxAttempts(unlimited));
        Assert.Equal(GradingMethod.Latest, MoodleScormSettings.Grading(unlimited));
    }

    [Fact]
    public void AScormScoreRequirement_BecomesTheMinimumScoreRule()
    {
        // Arrange
        var scorm = Activity("scorm", new() { ["maxgrade"] = "50", ["completionscorerequired"] = "40" });
        scorm.Completion = 2;

        // Act
        var (mode, passMark) = MoodleCompletion.ForScorm(scorm);

        // Assert
        Assert.Equal(CompletionModes.MinimumScore, mode);
        Assert.Equal(80, passMark);
    }

    [Theory]
    [InlineData("editingteacher", "Instructor")]
    [InlineData("teacher", "Assistant")]
    [InlineData("manager", "manager")]
    [InlineData(null, null)]
    public void ACompletionRole_MapsToACourseStaffRole(string moodleRole, string staffRole)
        => Assert.Equal(staffRole, MoodleCompletion.StaffRole(moodleRole));

    [Fact]
    public void Restrictions_ByEmailAndUserName_BecomePeople_ADateAnOpeningDate_AndTheRestIsReported()
    {
        // Arrange
        var json = """
            {"op":"&","c":[
              {"type":"profile","sf":"email","op":"isequalto","v":"ana@example.com"},
              {"type":"profile","sf":"username","op":"isequalto","v":"bo"},
              {"type":"profile","sf":"department","op":"isequalto","v":"Field"},
              {"type":"date","d":">=","t":1735689600},
              {"type":"group","id":4}
            ],"showc":[true,true,true,true,true]}
            """;

        // Act
        var restriction = MoodleRestrictions.Read(json);

        // Assert
        Assert.Equal([("email", "ana@example.com"), ("username", "bo")], restriction.People);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), restriction.OpensUtc);
        Assert.Equal(["profile:department", "group"], restriction.Unsupported);
        Assert.True(restriction.Any);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{\"op\":\"&\",\"c\":[]}")]
    [InlineData("not json")]
    public void NoRestriction_ReadsAsNone(string json)
        => Assert.False(MoodleRestrictions.Read(json).Any);

    [Fact]
    public void AnAssignment_KeepsItsDueDatePointsAndWhatLearnersHandIn()
    {
        // Arrange
        var assign = Activity("assign", new() { ["duedate"] = "1735689600", ["grade"] = "20", ["attemptreopenmethod"] = "manual", ["completionsubmit"] = "1" });
        assign.Records["plugin_config"] =
        [
            Record(("plugin", "onlinetext"), ("subtype", "assignsubmission"), ("name", "enabled"), ("value", "1")),
            Record(("plugin", "file"), ("subtype", "assignsubmission"), ("name", "enabled"), ("value", "1")),
            Record(("plugin", "file"), ("subtype", "assignsubmission"), ("name", "maxfilesubmissions"), ("value", "25")),
            Record(("plugin", "file"), ("subtype", "assignsubmission"), ("name", "maxsubmissionsizebytes"), ("value", "5242880")),
            Record(("plugin", "comments"), ("subtype", "assignfeedback"), ("name", "enabled"), ("value", "1")),
        ];

        // Act
        var assignment = MoodleAssignments.Read(assign);

        // Assert
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), assignment.DueUtc);
        Assert.Equal(20, assignment.MaxPoints);
        Assert.True(assignment.AcceptsText);
        Assert.True(assignment.AcceptsFiles);
        Assert.Equal(20, assignment.MaxFiles);
        Assert.Equal(5, assignment.MaxFileSizeMb);
        Assert.True(assignment.AllowResubmission);
        Assert.True(assignment.CompletesOnSubmit);
        Assert.False(assignment.UsesScale);
    }

    [Fact]
    public void AnAssignmentWithNothingToHandIn_IsASignOff_AndAScaleIsReported()
    {
        // Arrange
        var assign = Activity("assign", new() { ["grade"] = "-3" });

        // Act
        var assignment = MoodleAssignments.Read(assign);

        // Assert
        Assert.False(assignment.AcceptsText);
        Assert.False(assignment.AcceptsFiles);
        Assert.True(assignment.UsesScale);
        Assert.Equal(100, assignment.MaxPoints);
    }

    [Fact]
    public void AFeedback_BecomesASurvey_WithItsChoiceRatedAndWrittenQuestions_AndSkipsTheRest()
    {
        // Arrange
        var feedback = Activity("feedback", new() { ["anonymous"] = "1", ["multiple_submit"] = "1", ["timeclose"] = "1735689600" });
        feedback.Records["item"] =
        [
            Record(("typ", "multichoice"), ("name", "How did you hear of it?"), ("presentation", "r>>>>>Friend|Web|Other"), ("required", "1")),
            Record(("typ", "multichoice"), ("name", "What did you use?"), ("presentation", "c>>>>>Videos|Quizzes"), ("required", "0")),
            Record(("typ", "multichoicerated"), ("name", "How good was it?"), ("presentation", "r>>>>>0####Poor|1####Fair|2####Good"), ("required", "0")),
            Record(("typ", "textarea"), ("name", "What would you change?"), ("presentation", "30|5"), ("required", "0")),
            Record(("typ", "label"), ("name", ""), ("presentation", "<p>Thanks!</p>"), ("required", "0")),
            Record(("typ", "captcha"), ("name", "Captcha"), ("presentation", ""), ("required", "1")),
        ];

        // Act
        var survey = MoodleSurveys.FromFeedback(feedback);

        // Assert
        Assert.True(survey.Anonymous);
        Assert.True(survey.AllowChangeAnswer);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), survey.CloseUtc);
        Assert.Collection(
            survey.Questions,
            question => Assert.Equal(("How did you hear of it?", MoodleSurveyQuestionKind.SingleChoice, true), (question.Text, question.Kind, question.Required)),
            question => Assert.Equal(("What did you use?", MoodleSurveyQuestionKind.MultipleChoice), (question.Text, question.Kind)),
            question =>
            {
                Assert.Equal(MoodleSurveyQuestionKind.SingleChoice, question.Kind);
                Assert.Equal(["Poor", "Fair", "Good"], question.Options);
            },
            question => Assert.Equal(MoodleSurveyQuestionKind.Text, question.Kind));
        Assert.Equal(["Friend", "Web", "Other"], survey.Questions[0].Options);
        Assert.Equal(["captcha"], survey.Skipped);
    }

    [Fact]
    public void AChoice_BecomesASurveyOfOneQuestion_WithItsOptionsAndWhenItShowsTheTotals()
    {
        // Arrange
        var choice = Activity("choice", new() { ["intro"] = "<p>Which day suits you?</p>", ["allowmultiple"] = "0", ["allowupdate"] = "1", ["showresults"] = "2", ["publish"] = "0", ["timeclose"] = "0" });
        choice.Records["option"] = [Record(("text", "Monday")), Record(("text", "Friday"))];

        // Act
        var survey = MoodleSurveys.FromChoice(choice);

        // Assert
        var question = Assert.Single(survey.Questions);
        Assert.Equal("Which day suits you?", question.Text);
        Assert.Equal(MoodleSurveyQuestionKind.SingleChoice, question.Kind);
        Assert.Equal(["Monday", "Friday"], question.Options);
        Assert.True(question.Required);
        Assert.True(survey.AllowChangeAnswer);
        Assert.True(survey.Anonymous);
        Assert.Equal(MoodleSurveyResults.AfterClose, survey.Results);
        Assert.Null(survey.CloseUtc);
    }

    [Fact]
    public void PluginFileLinks_PointAtTheImportedPictures_AndTheOthersAreLeftOut()
    {
        // Arrange
        var html = "<p>Hi <img src=\"@@PLUGINFILE@@/sub/Exit%20sign.png\" alt=\"exit\"> and <img src=\"@@PLUGINFILE@@/missing.png\"> <a href=\"@@PLUGINFILE@@/sub/Exit%20sign.png\">open</a></p>";
        var urls = new Dictionary<string, string>(StringComparer.Ordinal) { ["/sub/Exit sign.png"] = "/media/mediafields/Course/c1/exit-sign.png" };

        // Act
        var rewritten = MoodleFiles.Rewrite(html, urls);

        // Assert
        Assert.Contains("<img src=\"/media/mediafields/Course/c1/exit-sign.png\" alt=\"exit\">", rewritten, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/media/mediafields/Course/c1/exit-sign.png\">open</a>", rewritten, StringComparison.Ordinal);
        Assert.DoesNotContain("@@PLUGINFILE@@", rewritten, StringComparison.Ordinal);
        Assert.DoesNotContain("missing.png", rewritten, StringComparison.Ordinal);
        Assert.Equal("/sub/Exit sign.png", MoodleFiles.PathOf(new MoodleFile { FilePath = "/sub/", FileName = "Exit sign.png" }));
    }

    private static MoodleActivity Activity(string module, Dictionary<string, string> settings)
    {
        var activity = new MoodleActivity { ModuleId = "1", ModuleName = module, Name = module };

        foreach (var (key, value) in settings)
        {
            if (value is not null)
            {
                activity.Settings[key] = value;
            }
        }

        return activity;
    }

    private static Dictionary<string, string> Record(params (string Key, string Value)[] values)
        => values.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);
}
