using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Coursework;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.Surveys;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Models;
using OrchardCore.Media;
using OrchardCore.Media.Fields;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Remaining work section 3, items 11 to 13, on a running site: Media's own limits and original file names, the
/// coursework sign-off, the IMS content package, and the feedback survey and choice.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed partial class LearningActivitiesTests
{
    private readonly LearningSiteFixture _fixture;

    public LearningActivitiesTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Media_KeepsItsOwnLimits_WithLearningOn()
    {
        // Act
        var options = await _fixture.InTenantAsync(services => Task.FromResult(services.GetRequiredService<IOptions<MediaOptions>>().Value));

        // Assert: review round 11, R3. Media's limits are Media's configuration; Learning adds no file type and no size.
        Assert.True(options.MaxFileSize < 1_000_000_000, $"MaxFileSize is {options.MaxFileSize}.");
        Assert.DoesNotContain(".ods", options.AllowedFileExtensions);
        Assert.DoesNotContain(".exe", options.AllowedFileExtensions);
    }

    [Fact]
    public async Task FileLesson_ShowsLearnersTheOriginalFileName()
    {
        // Arrange
        var (courseId, itemIds) = await CreateCourseAsync(async (services, course) =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var lesson = await NewItemAsync(contentManager, course, LearningConstants.ContentTypes.FileLesson, "Handouts", CompletionModes.Viewed);

            lesson.Alter<ContentPart>(LearningConstants.ContentTypes.FileLesson, part => part.Alter<MediaField>("Files", field =>
            {
                field.Paths = ["mediafields/FileLesson/x/3f2504e04f8911d39a0c0305e82c3301_Safety plan.ods", "mediafields/FileLesson/x/1a2b3c4d.pdf"];
                field.SetAttachedFileNames(["", "Driver handbook.pdf"]);
            }));
            await contentManager.CreateAsync(lesson, VersionOptions.Published);

            return [lesson.ContentItemId];
        });

        // Act
        using var learner = await _fixture.CreateClientAsync("learner1");
        var player = await learner.GetStringAsync($"learning/courses/{courseId}/play/{itemIds[0]}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(">Safety plan.ods</a>", player);
        Assert.Contains(">Driver handbook.pdf</a>", player);
        Assert.DoesNotContain(">mediafields/FileLesson", player);
    }

    [Fact]
    public async Task SignOff_TheGraderOpensALearner_FromTheSubmissionsPage_AndGradesWithNothingHandedIn()
    {
        // Arrange
        var (courseId, itemIds) = await CreateCourseAsync(async (services, course) =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var item = await NewItemAsync(contentManager, course, CourseworkConstants.ContentTypes.Coursework, "Forklift practical", CompletionModes.Graded);

            item.Alter<CourseworkPart>(part =>
            {
                part.SubmissionType = CourseworkSubmissionType.SignOff;
                part.MaxPoints = 10;
            });
            item.Alter<CourseItemPart>(part => part.Gradable = true);
            await contentManager.CreateAsync(item, VersionOptions.Published);

            return [item.ContentItemId];
        });
        var courseworkId = itemIds[0];

        using var admin = await _fixture.CreateClientAsync("admin");
        var list = await admin.GetStringAsync($"Admin/learning/coursework/{courseworkId}/submissions", TestContext.Current.CancellationToken);
        var enrollmentId = await EnrollmentIdAsync(courseId);

        // Act
        var signOff = await admin.PostAsync($"Admin/learning/coursework/{courseworkId}/sign-off", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(list),
            ["enrollmentId"] = enrollmentId,
        }), TestContext.Current.CancellationToken);
        var gradingUrl = signOff.Headers.Location?.OriginalString;
        var grading = gradingUrl is null ? null : await admin.GetStringAsync(gradingUrl.TrimStart('/'), TestContext.Current.CancellationToken);

        using var learner = await _fixture.CreateClientAsync("learner1");
        var player = await learner.GetStringAsync($"learning/courses/{courseId}/play/{courseworkId}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("data-coursework-sign-off", list);
        Assert.Contains("learner1", list);
        Assert.Equal(HttpStatusCode.Redirect, signOff.StatusCode);
        Assert.Contains("coursework/submissions/", gradingUrl);
        Assert.Contains("A sign-off", grading);
        Assert.DoesNotContain("Return for resubmission", grading);
        Assert.Contains("your instructor signs this off", player);
        Assert.DoesNotContain("name=\"files\"", player);
    }

    [Fact]
    public async Task ImsContentPackage_ImportsLaunchesWithoutTheRuntime_AndCompletesWhenOpened()
    {
        // Arrange
        var (courseId, itemIds) = await CreateCourseAsync(async (services, course) =>
        {
            using var zip = new MemoryStream(ImsPackage());
            var imported = await services.GetRequiredService<LearningPackageImporter>().ImportAsync("handbook.zip", zip);

            Assert.True(imported.Succeeded, imported.Error);

            var contentManager = services.GetRequiredService<IContentManager>();
            var item = await NewItemAsync(contentManager, course, PackagesConstants.ContentTypes.Package, "Handbook", PackageImportResult.CompletionModeFor(imported.Manifest.Standard));

            item.Alter<LearningPackagePart>(part => imported.ApplyTo(part, "handbook.zip", "admin", DateTime.UtcNow));
            await contentManager.CreateAsync(item, VersionOptions.Published);

            return [item.ContentItemId];
        });
        var itemId = itemIds[0];

        using var learner = await _fixture.CreateClientAsync("learner1");
        var player = await learner.GetStringAsync($"learning/courses/{courseId}/play/{itemId}", TestContext.Current.CancellationToken);

        // Act
        var launch = await learner.PostAsync($"learning/packages/{itemId}/launch", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(player),
        }), TestContext.Current.CancellationToken);
        var launched = JsonNode.Parse(await launch.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject();
        var launchUrl = launched["launchUrl"]!.GetValue<string>();
        var page = await learner.GetStringAsync(launchUrl.TrimStart('/'), TestContext.Current.CancellationToken);

        var heartbeat = await learner.PostAsync($"learning/progress/{await EnrollmentIdAsync(courseId)}/{itemId}/heartbeat", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(player),
            ["seconds"] = "0",
        }), TestContext.Current.CancellationToken);
        var progress = await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<ILearnerProgressService>().GetAsync(await EnrollmentIdAsync(courseId)))?.Find(itemId));

        // Assert
        Assert.Contains("This package does not track your progress", player);
        Assert.DoesNotContain("The package reports your progress itself", player);
        Assert.True(launched["launched"]!.GetValue<bool>(), launched["message"]?.GetValue<string>());
        Assert.Contains("pages/intro.html", launchUrl);
        Assert.Contains("Forklift handbook intro", page);
        Assert.DoesNotContain("CrestAppsScorm12Launch", page);
        Assert.Equal(HttpStatusCode.OK, heartbeat.StatusCode);
        Assert.Equal(CourseItemState.Completed, progress?.State);
    }

    [Fact]
    public async Task FeedbackSurvey_ALearnerAnswersOnTheSurveysRunner_TheItemCompletes_AndStaffSeeTheResponse()
    {
        // Arrange
        string surveyId = null;
        var (courseId, itemIds) = await CreateCourseAsync(async (services, course) =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var item = await NewItemAsync(contentManager, course, LearningSurveysConstants.ContentTypes.FeedbackSurvey, "Course feedback", CompletionModes.Submitted);

            item.Alter<CourseSurveyPart>(_ => { });
            await contentManager.CreateAsync(item, VersionOptions.Published);

            surveyId = LearningSurveyTestSupport.SurveyIdOf(item);
            await LearningSurveyTestSupport.PublishAsync(services, surveyId, LearningSurveyTestSupport.Definition(
                "Course feedback",
                LearningSurveyTestSupport.SingleChoice("again", "Would you take it again?", ("yes", "Yes"), ("no", "No")),
                new SurveyQuestion { Id = "change", Name = "change", ControlType = SurveyControlNames.LongText, Title = "What should change?" }));

            return [item.ContentItemId];
        });
        var itemId = itemIds[0];
        var comment = "More practice " + Guid.NewGuid().ToString("n")[..6];

        using var learner = await _fixture.CreateClientAsync("learner1");
        var player = await learner.GetStringAsync($"learning/courses/{courseId}/play/{itemId}", TestContext.Current.CancellationToken);

        // Act
        await LearningSurveyTestSupport.AnswerOverHttpAsync(learner, $"learning/surveys/{itemId}/start", player, new Dictionary<string, string>
        {
            ["q.again"] = "yes",
            ["q.change"] = comment,
        });
        var after = await learner.GetStringAsync($"learning/courses/{courseId}/play/{itemId}", TestContext.Current.CancellationToken);
        var enrollmentId = await EnrollmentIdAsync(courseId);
        var progress = await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<ILearnerProgressService>().GetAsync(enrollmentId))?.Find(itemId));
        var responses = await _fixture.InTenantAsync(services =>
            services.GetRequiredService<ILearningSurveyService>().ListEnrollmentResponsesAsync(surveyId, enrollmentId));

        using var admin = await _fixture.CreateClientAsync("admin");
        var staffList = await admin.GetAsync($"Admin/surveys/{surveyId}/responses", TestContext.Current.CancellationToken);
        var refused = await learner.GetAsync($"Admin/surveys/{surveyId}/responses", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("data-learning-survey", player);
        Assert.Contains("You answered on", after);
        Assert.Equal(CourseItemState.Completed, progress?.State);
        var response = Assert.Single(responses);
        Assert.Equal(SurveyResponseStatus.Completed, response.Status);
        Assert.Equal(comment, Assert.Single(response.Answers["change"].Values));
        Assert.Equal(HttpStatusCode.OK, staffList.StatusCode);
        Assert.Contains(response.Number, await staffList.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.NotEqual(HttpStatusCode.OK, refused.StatusCode);
    }

    [Fact]
    public async Task Choice_ShowsTheTotalsToALearnerOnceAnswered()
    {
        // Arrange
        var (courseId, itemIds) = await CreateCourseAsync(async (services, course) =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var item = await NewItemAsync(contentManager, course, LearningSurveysConstants.ContentTypes.Choice, "Training day", CompletionModes.Submitted);

            item.Alter<CourseSurveyPart>(_ => { });
            await contentManager.CreateAsync(item, VersionOptions.Published);

            var definition = LearningSurveyTestSupport.Definition(
                "Training day",
                LearningSurveyTestSupport.SingleChoice("day", "Which day suits you?", ("mon", "Monday"), ("fri", "Friday")));
            definition.Review.ResultsVisibility = ResultsVisibility.AfterAnswer;
            await LearningSurveyTestSupport.PublishAsync(services, LearningSurveyTestSupport.SurveyIdOf(item), definition);

            return [item.ContentItemId];
        });
        var choiceId = itemIds[0];

        using var learner = await _fixture.CreateClientAsync("learner1");
        var before = await learner.GetStringAsync($"learning/courses/{courseId}/play/{choiceId}", TestContext.Current.CancellationToken);

        // Act
        var done = await LearningSurveyTestSupport.AnswerOverHttpAsync(learner, $"learning/surveys/{choiceId}/start", before, new Dictionary<string, string>
        {
            ["q.day"] = "fri",
        });
        var ending = await learner.GetStringAsync(done, TestContext.Current.CancellationToken);
        var resultsPath = ResultsLinkPattern().Match(ending) is { Success: true } match ? match.Groups["path"].Value : null;
        var results = resultsPath is null ? null : await learner.GetStringAsync(resultsPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(resultsPath);
        Assert.Contains("Friday", results);
        Assert.Contains("Monday", results);
    }

    private async Task<string> EnrollmentIdAsync(string courseId)
        => await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<IEnrollmentStore>().ListByCourseForUserAsync(_fixture.EnrolledUserId, courseId))[0].ItemId);

    // A course of its own with the given items in one section and learner1 enrolled, so the fixture's course keeps its outline.
    private async Task<(string CourseId, string[] ItemIds)> CreateCourseAsync(Func<IServiceProvider, ContentItem, Task<string[]>> addItems)
        => await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);
            var title = "Activities course " + Guid.NewGuid().ToString("n")[..6];

            course.DisplayText = title;
            course.Alter<TitlePart>(part => part.Title = title);
            course.Alter<CoursePart>(part => part.EnrollmentMode = EnrollmentMode.Open);
            await contentManager.CreateAsync(course, VersionOptions.Draft);

            var itemIds = await addItems(services, course);

            course.Alter<CourseOutlinePart>(part => part.Sections =
            [
                new CourseSection { Id = "s1", Title = "Activities", Items = [.. itemIds.Select(id => new CourseOutlineItem { ContentItemId = id })] },
            ]);
            await contentManager.UpdateAsync(course);
            await contentManager.PublishAsync(course);

            var enrolled = await services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
            {
                UserId = _fixture.EnrolledUserId,
                CourseId = course.ContentItemId,
                Source = LearningConstants.EnrollmentSources.Manual,
                SourceId = "activities-test",
            });

            Assert.True(enrolled.Succeeded, enrolled.ErrorCode);

            return (course.ContentItemId, itemIds);
        });

    private static async Task<ContentItem> NewItemAsync(IContentManager contentManager, ContentItem course, string contentType, string title, string completionMode)
    {
        var item = await contentManager.NewAsync(contentType);

        item.DisplayText = title;
        item.Alter<TitlePart>(part => part.Title = title);
        item.Alter<ContainedPart>(part =>
        {
            part.ListContentItemId = course.ContentItemId;
            part.ListContentType = course.ContentType;
        });
        item.Alter<CourseItemPart>(part => part.CompletionMode = completionMode);

        return item;
    }

    private static byte[] ImsPackage()
    {
        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, "imsmanifest.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <manifest identifier="it.imscp" version="1.1" xmlns="http://www.imsglobal.org/xsd/imscp_v1p1">
                  <metadata><schema>IMS Content</schema><schemaversion>1.1.4</schemaversion></metadata>
                  <organizations default="TOC1">
                    <organization identifier="TOC1">
                      <title>Forklift handbook</title>
                      <item identifier="I0" identifierref="R0"><title>Start here</title></item>
                    </organization>
                  </organizations>
                  <resources>
                    <resource identifier="R0" type="webcontent" href="pages/intro.html"><file href="pages/intro.html"/></resource>
                  </resources>
                </manifest>
                """);
            Add(archive, "pages/intro.html", "<!DOCTYPE html><html><head><title>Intro</title></head><body>Forklift handbook intro</body></html>");
        }

        return stream.ToArray();
    }

    private static void Add(ZipArchive archive, string name, string content)
    {
        using var writer = archive.CreateEntry(name).Open();
        writer.Write(Encoding.UTF8.GetBytes(content));
    }

    [GeneratedRegex("href=\"(?<path>[^\"]*/s/(?:r/)?[^\"/]+/results)\"")]
    private static partial Regex ResultsLinkPattern();
}
