using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework;
using CrestApps.OrchardCore.Learning.Coursework.Drivers;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using CrestApps.OrchardCore.Learning.Coursework.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Coursework;

/// <summary>
/// Review round 8, 7.5: the grading rules hold on the server, whatever the page read before the lock or the browser
/// let through. Grading graded work again, or returning it, needs Override grades on the fresh read; feedback and the
/// reason have the page's length limits; a rubric with two criteria of the same id neither saves nor breaks grading; and
/// points typed as "7,5" or "7.5" mean seven and a half in any culture.
/// </summary>
public sealed class GradingRulesTests
{
    private const string Course = "course1";
    private const string Work = "essay1";

    [Fact]
    public async Task Regrade_WithoutOverrideGrades_IsRefused_OnTheFreshRead()
    {
        // Arrange: the page read the work before another grader graded it.
        await using var h = await CreateAsync();
        await h.EnrollAsync("ana", Course);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");
        await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 60);

        // Act
        var regraded = await GradeAsync(h, submitted.Submission.ItemId, request =>
        {
            request.Points = 90;
            request.Reason = "Second look";
            request.CanOverrideGrades = false;
        });
        var stored = await h.FindAsync(submitted.Submission.ItemId);

        // Assert
        Assert.Equal(SubmissionErrors.OverrideRequired, regraded.Error);
        Assert.Equal(60m, stored.Score);
        Assert.Empty(stored.History);
    }

    [Fact]
    public async Task Grade_OfUngradedWork_WithoutOverrideGrades_IsAllowed()
    {
        // Arrange
        await using var h = await CreateAsync();
        await h.EnrollAsync("ana", Course);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");

        // Act
        var graded = await GradeAsync(h, submitted.Submission.ItemId, request =>
        {
            request.Points = 70;
            request.CanOverrideGrades = false;
        });

        // Assert
        Assert.True(graded.Succeeded, graded.Error);
    }

    [Fact]
    public async Task Return_OfGradedWork_WithoutOverrideGrades_IsRefused_OnTheFreshRead()
    {
        // Arrange
        await using var h = await CreateAsync();
        await h.EnrollAsync("ana", Course);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");
        await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 60);

        // Act
        var returned = await h.RunAsync(services => services.GetRequiredService<ISubmissionService>()
            .ReturnAsync(submitted.Submission.ItemId, "Add the references.", "grader2", canOverrideGrades: false));
        var stored = await h.FindAsync(submitted.Submission.ItemId);

        // Assert
        Assert.Equal(SubmissionErrors.OverrideRequired, returned.Error);
        Assert.Equal(SubmissionStatus.Graded, stored.Status);
    }

    [Fact]
    public async Task FeedbackAndReason_LongerThanThePageAllows_AreRefused()
    {
        // Arrange
        await using var h = await CreateAsync();
        await h.EnrollAsync("ana", Course);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");

        // Act
        var longFeedback = await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 60, feedback: new string('a', CourseworkConstants.MaxFeedbackLength + 1));
        var graded = await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 60, feedback: new string('a', CourseworkConstants.MaxFeedbackLength));
        var longReason = await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 70, reason: new string('r', CourseworkConstants.MaxReasonLength + 1));
        var longReturn = await h.ReturnAsync(submitted.Submission.ItemId, "grader", feedback: new string('a', CourseworkConstants.MaxFeedbackLength + 1));
        var stored = await h.FindAsync(submitted.Submission.ItemId);

        // Assert
        Assert.Equal(SubmissionErrors.FeedbackTooLong, longFeedback.Error);
        Assert.True(graded.Succeeded, graded.Error);
        Assert.Equal(SubmissionErrors.ReasonTooLong, longReason.Error);
        Assert.Equal(SubmissionErrors.FeedbackTooLong, longReturn.Error);
        Assert.Equal(60m, stored.Score);
        Assert.Equal(SubmissionStatus.Graded, stored.Status);
    }

    [Fact]
    public async Task Grade_AgainstARubricWithTwoCriteriaOfOneId_DoesNotBreak()
    {
        // Arrange: a rubric saved without the editor (a recipe, the content API) repeats a criterion id.
        await using var h = await CreateAsync(part => part.RubricId = "rubric1");
        h.AddRubric("rubric1", ("argument", [("weak", 1m), ("strong", 4m)]), ("argument", [("weak", 0m), ("strong", 6m)]));
        await h.EnrollAsync("ana", Course);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");

        // Act
        var graded = await h.GradeAsync(submitted.Submission.ItemId, "grader", choices: new Dictionary<string, string> { ["argument"] = "strong" });

        // Assert
        Assert.True(graded.Succeeded, graded.Error);
        Assert.Equal("strong", Assert.Single(graded.Submission.RubricChoices).Value);
    }

    [Fact]
    public async Task TheRubricEditor_RefusesTwoCriteriaWithOneId()
    {
        // Arrange: the editor posts the ids of the criteria back, so a doctored post can repeat one.
        var driver = new RubricPartDisplayDriver(new PassThroughLocalizer());
        var posted = new RubricPartViewModel
        {
            Criteria =
            [
                new RubricCriterionRow { Id = "c1", Title = "Argument", Levels = [new RubricLevelRow { Title = "Strong", Points = 4 }] },
                new RubricCriterionRow { Id = "c1", Title = "Sources", Levels = [new RubricLevelRow { Title = "Many", Points = 6 }] },
            ],
        };
        var updater = PostedFormUpdateModel.CreateContext(posted);
        var context = new UpdatePartEditorContext(
            new ContentTypePartDefinition(nameof(RubricPart), new ContentPartDefinition(nameof(RubricPart)), new JsonObject()),
            updater);

        // Act
        await driver.UpdateAsync(new RubricPart(), context);

        // Assert
        Assert.False(updater.Updater.ModelState.IsValid);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public void Points_TypedWithACommaOrAPoint_MeanTheSameInAnyCulture(string culture)
    {
        // Arrange
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);

        try
        {
            // Act and assert
            Assert.True(GradePoints.TryParse("7,5", out var comma));
            Assert.Equal(7.5m, comma);
            Assert.True(GradePoints.TryParse("7.5", out var point));
            Assert.Equal(7.5m, point);
            Assert.True(GradePoints.TryParse(" 10 ", out var whole));
            Assert.Equal(10m, whole);
            Assert.True(GradePoints.TryParse(string.Empty, out var none));
            Assert.Null(none);
            Assert.False(GradePoints.TryParse("seven", out _));
            Assert.False(GradePoints.TryParse("1.000,5", out _));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static Task<SubmissionResult> GradeAsync(CourseworkTestHarness h, string submissionId, Action<GradeSubmissionRequest> configure)
        => h.RunAsync(services =>
        {
            var request = new GradeSubmissionRequest { SubmissionId = submissionId, Actor = "grader2" };
            configure(request);

            return services.GetRequiredService<ISubmissionService>().GradeAsync(request);
        });

    private static async Task<CourseworkTestHarness> CreateAsync(Action<CourseworkPart> configure = null)
    {
        var h = await CourseworkTestHarness.CreateAsync();

        h.Courses.AddCourse(Course);
        h.Courses.AddItem(Course, "lesson1");
        h.AddCoursework(Course, Work, part =>
        {
            part.SubmissionType = CourseworkSubmissionType.Text;
            part.MaxPoints = 100;
            configure?.Invoke(part);
        });

        return h;
    }

    private sealed class PassThroughLocalizer : IStringLocalizer<RubricPartDisplayDriver>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
