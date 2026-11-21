using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Html.Models;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// A course whose learners take the items in order, on a running site: the progress post, the package launch and the
/// quiz start of an item after an unfinished required item are refused, the player explains the lock and links to the
/// item to finish, and completing that item opens the next one.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class ItemsInOrderTests
{
    private const string Manifest = """
        <?xml version="1.0" encoding="UTF-8"?>
        <manifest identifier="order.package" version="1.0" xmlns="http://www.imsproject.org/xsd/imscp_rootv1p1p2" xmlns:adlcp="http://www.adlnet.org/xsd/adlcp_rootv1p2">
          <metadata><schema>ADL SCORM</schema><schemaversion>1.2</schemaversion></metadata>
          <organizations default="O">
            <organization identifier="O">
              <title>Order package</title>
              <item identifier="I1" identifierref="R1"><title>Order lesson</title></item>
            </organization>
          </organizations>
          <resources>
            <resource identifier="R1" type="webcontent" adlcp:scormtype="sco" href="index.html"><file href="index.html"/></resource>
          </resources>
        </manifest>
        """;

    private readonly LearningSiteFixture _fixture;

    public ItemsInOrderTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ALockedItem_RefusesProgressLaunchAndQuizStart_UntilTheItemBeforeItIsComplete()
    {
        // Arrange
        var course = await CreateCourseAsync();
        using var learner = await _fixture.CreateClientAsync("learner1");
        var firstPage = await learner.GetStringAsync($"learning/courses/{course.CourseId}/play/{course.PageId}", TestContext.Current.CancellationToken);
        var token = LearningSiteFixture.Token(firstPage);

        // Act
        var lockedPlayer = await learner.GetStringAsync($"learning/courses/{course.CourseId}/play/{course.PackageId}", TestContext.Current.CancellationToken);
        var heartbeat = await PostAsync(learner, $"learning/progress/{course.EnrollmentId}/{course.PackageId}/heartbeat", token, ("seconds", "0"));
        var launch = await PostAsync(learner, $"learning/packages/{course.PackageId}/launch", token);
        var quizStart = await PostAsync(learner, $"learning/quizzes/{course.QuizId}/start", token);

        // Assert
        Assert.Contains("data-learning-locked-item", lockedPlayer);
        Assert.Contains($"play/{course.PageId}\" data-learning-blocking-item", lockedPlayer);
        Assert.Equal(HttpStatusCode.NotFound, heartbeat.StatusCode);
        Assert.False(await LaunchedAsync(launch));
        Assert.DoesNotContain("/s/r/", quizStart.Headers.Location?.OriginalString ?? string.Empty);

        // Completing the first item opens the package; the quiz still waits for the package.
        var done = await PostAsync(learner, $"learning/progress/{course.EnrollmentId}/{course.PageId}/done", token);

        Assert.Equal(HttpStatusCode.Redirect, done.StatusCode);
        Assert.True(await LaunchedAsync(await PostAsync(learner, $"learning/packages/{course.PackageId}/launch", token)));
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(learner, $"learning/progress/{course.EnrollmentId}/{course.PackageId}/heartbeat", token, ("seconds", "0"))).StatusCode);
        Assert.DoesNotContain("/s/r/", (await PostAsync(learner, $"learning/quizzes/{course.QuizId}/start", token)).Headers.Location?.OriginalString ?? string.Empty);
    }

    private static async Task<bool> LaunchedAsync(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return false;
        }

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return json?["launched"]?.GetValue<bool>() == true;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string url, string token, params (string Key, string Value)[] fields)
    {
        var values = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token) };

        values.AddRange(fields.Select(field => new KeyValuePair<string, string>(field.Key, field.Value)));

        return client.PostAsync(url, new FormUrlEncodedContent(values), TestContext.Current.CancellationToken);
    }

    private static byte[] Package()
    {
        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in new[] { ("imsmanifest.xml", Manifest.TrimStart()), ("index.html", "<!DOCTYPE html><html><head><title>Order</title></head><body>Order lesson</body></html>") })
            {
                using var writer = archive.CreateEntry(name).Open();
                writer.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        return stream.ToArray();
    }

    private Task<(string CourseId, string PageId, string PackageId, string QuizId, string EnrollmentId)> CreateCourseAsync()
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var suffix = Guid.NewGuid().ToString("n")[..6];
            var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);

            course.DisplayText = "In order " + suffix;
            course.Alter<TitlePart>(part => part.Title = course.DisplayText);
            course.Alter<CoursePart>(part =>
            {
                part.EnrollmentMode = EnrollmentMode.Open;
                part.ItemsInOrder = true;
            });
            await contentManager.CreateAsync(course, VersionOptions.Draft);

            async Task<ContentItem> ItemAsync(string type, string title, string completionMode, Action<ContentItem> configure = null)
            {
                var item = await contentManager.NewAsync(type);

                item.DisplayText = title;
                item.Alter<TitlePart>(part => part.Title = title);
                item.Alter<HtmlBodyPart>(part => part.Html = $"<p>{title}</p>");
                configure?.Invoke(item);
                item.Alter<CourseItemPart>(part => part.CompletionMode = completionMode);
                item.Alter<ContainedPart>(part =>
                {
                    part.ListContentItemId = course.ContentItemId;
                    part.ListContentType = course.ContentType;
                });
                await contentManager.CreateAsync(item, VersionOptions.Published);

                return item;
            }

            var page = await ItemAsync(LearningConstants.ContentTypes.PageLesson, "Read first", CompletionModes.MarkedDone);

            using var zip = new MemoryStream(Package());
            var imported = await services.GetRequiredService<LearningPackageImporter>().ImportAsync("order.zip", zip);

            Assert.True(imported.Succeeded, imported.Error);

            var package = await ItemAsync(PackagesConstants.ContentTypes.Package, "Then the package", CompletionModes.PackageReported, item =>
                item.Alter<LearningPackagePart>(part => imported.ApplyTo(part, "order.zip", "admin", DateTime.UtcNow)));

            var quiz = await ItemAsync(AssessmentsConstants.ContentTypes.Quiz, "Last the quiz", CompletionModes.Submitted, item =>
                item.Alter<QuizPart>(part => part.GradingMethod = GradingMethod.Highest));

            // The quiz's questions live in the survey it owns; publishing it lets a learner start an attempt.
            await LearningSurveyTestSupport.PublishAsync(services, LearningSurveyTestSupport.SurveyIdOf(quiz), LearningSurveyTestSupport.Definition(
                "Order quiz " + suffix,
                LearningSurveyTestSupport.SingleChoice("order", "Items open in order.", ("yes", "True"), ("no", "False"))));

            course.Alter<CourseOutlinePart>(part => part.Sections =
            [
                new CourseSection
                {
                    Id = "s1",
                    Title = "In order",
                    Items =
                    [
                        new CourseOutlineItem { ContentItemId = page.ContentItemId },
                        new CourseOutlineItem { ContentItemId = package.ContentItemId },
                        new CourseOutlineItem { ContentItemId = quiz.ContentItemId },
                    ],
                },
            ]);
            await contentManager.UpdateAsync(course);
            await contentManager.PublishAsync(course);

            var enrolled = await services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
            {
                UserId = _fixture.EnrolledUserId,
                CourseId = course.ContentItemId,
                Source = LearningConstants.EnrollmentSources.Manual,
                SourceId = "in-order-" + suffix,
            });

            Assert.True(enrolled.Succeeded, enrolled.ErrorCode);

            return (course.ContentItemId, page.ContentItemId, package.ContentItemId, quiz.ContentItemId, enrolled.Enrollment.ItemId);
        });
}
