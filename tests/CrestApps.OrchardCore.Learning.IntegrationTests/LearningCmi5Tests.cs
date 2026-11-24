using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// A cmi5 course on a running site: the learner launches an AU from the player, its page is served sandboxed without
/// the SCORM shim, the fetch URL hands the auth token out once to any origin, and the token opens the LRS for the
/// launch data and the statements of that registration.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LearningCmi5Tests
{
    private const string CourseStructure = """
        <?xml version="1.0" encoding="utf-8"?>
        <courseStructure xmlns="https://w3id.org/xapi/profiles/cmi5/v1/CourseStructure.xsd">
          <course id="https://example.com/it/cmi5-course">
            <title><langstring lang="en-US">Integration cmi5 course</langstring></title>
            <description><langstring lang="en-US">A course for the integration test.</langstring></description>
          </course>
          <au id="https://example.com/it/cmi5-course/au/1" moveOn="CompletedOrPassed" masteryScore="0.7">
            <title><langstring lang="en-US">Lesson one</langstring></title>
            <description><langstring lang="en-US">The first lesson.</langstring></description>
            <url>au/index.html</url>
          </au>
        </courseStructure>
        """;

    private readonly LearningSiteFixture _fixture;

    public LearningCmi5Tests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Learner_LaunchesAnAu_FetchesTheTokenOnce_AndTheTokenOpensTheLaunchDataAndStatements()
    {
        // Arrange
        var (courseId, itemId) = await CreateCourseAsync("learner2");
        using var learner = await _fixture.CreateClientAsync("learner2");
        using var anonymous = new HttpClient { BaseAddress = _fixture.BaseAddress };
        var player = await learner.GetStringAsync($"learning/courses/{courseId}/play/{itemId}", TestContext.Current.CancellationToken);

        // Act: launch
        var launched = await learner.PostAsync($"learning/packages/{itemId}/cmi5/launch", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(player),
            ["au"] = "0",
        }), TestContext.Current.CancellationToken);
        var launch = JsonNode.Parse(await launched.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject();
        var launchUrl = launch["launchUrl"]!.GetValue<string>();
        var parameters = launchUrl[(launchUrl.IndexOf('?') + 1)..].Split('&').Select(pair => pair.Split('=', 2)).ToDictionary(pair => pair[0], pair => Uri.UnescapeDataString(pair[1]));

        // The AU page, as the sandboxed frame loads it: no cookie, no SCORM shim.
        var page = await anonymous.GetAsync(launchUrl.TrimStart('/'), TestContext.Current.CancellationToken);
        var pageText = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // The fetch URL, from the frame's opaque origin.
        var fetch = new HttpRequestMessage(HttpMethod.Post, parameters["fetch"]);

        fetch.Headers.Add("Origin", "null");

        var fetched = await anonymous.SendAsync(fetch, TestContext.Current.CancellationToken);
        var token = JsonNode.Parse(await fetched.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!["auth-token"]!.GetValue<string>();
        var again = JsonNode.Parse(await (await anonymous.PostAsync(parameters["fetch"], null, TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        using var au = new HttpClient { BaseAddress = _fixture.BaseAddress };

        au.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        au.DefaultRequestHeaders.Add("X-Experience-API-Version", "1.0.3");

        var launchData = await au.GetAsync("learning/xapi/activities/state?" + Query(("activityId", parameters["activityId"]), ("agent", parameters["actor"]), ("registration", parameters["registration"]), ("stateId", "LMS.LaunchData")), TestContext.Current.CancellationToken);
        var statements = await au.GetAsync("learning/xapi/statements?" + Query(("registration", parameters["registration"])), TestContext.Current.CancellationToken);
        var otherState = await au.GetAsync("learning/xapi/activities/state?" + Query(("activityId", "http://example.com/other"), ("agent", parameters["actor"]), ("registration", parameters["registration"]), ("stateId", "x")), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(launch["launched"]!.GetValue<bool>(), launch["message"]?.GetValue<string>());
        Assert.False(launch["ownWindow"]!.GetValue<bool>());
        Assert.Contains("data-cmi5-launch-url", player);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("sandbox allow-scripts allow-forms allow-popups", page.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("Lesson one page", pageText);
        Assert.DoesNotContain("crestapps-scorm", pageText);
        Assert.Equal(_fixture.BaseAddress.ToString().TrimEnd('/') + "/learning/xapi/", parameters["endpoint"]);
        Assert.Null(JsonNode.Parse(parameters["actor"])!["mbox"]);
        Assert.Equal("*", fetched.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("1", again!["error-code"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, launchData.StatusCode);
        Assert.Equal("Normal", JsonNode.Parse(await launchData.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!["launchMode"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, statements.StatusCode);
        Assert.Contains("http://adlnet.gov/expapi/verbs/launched", await statements.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.Forbidden, otherState.StatusCode);
    }

    private static string Query(params (string Name, string Value)[] values)
        => string.Join("&", values.Select(value => value.Name + "=" + Uri.EscapeDataString(value.Value)));

    private static byte[] Cmi5Package()
    {
        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = archive.CreateEntry("cmi5.xml").Open())
            {
                writer.Write(Encoding.UTF8.GetBytes(CourseStructure.TrimStart()));
            }

            using (var writer = archive.CreateEntry("au/index.html").Open())
            {
                writer.Write(Encoding.UTF8.GetBytes("<!DOCTYPE html><html><head><title>Lesson one</title></head><body>Lesson one page</body></html>"));
            }
        }

        return stream.ToArray();
    }

    private async Task<(string CourseId, string ItemId)> CreateCourseAsync(string learner)
    {
        var userId = await _fixture.InTenantAsync(async services =>
        {
            var user = await services.GetRequiredService<global::OrchardCore.Users.Services.IUserService>().GetUserAsync(learner) as global::OrchardCore.Users.Models.User;

            return user!.UserId;
        });

        return await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);
            var title = "cmi5 course " + Guid.NewGuid().ToString("n")[..6];

            course.DisplayText = title;
            course.Alter<TitlePart>(part => part.Title = title);
            course.Alter<CoursePart>(part => part.EnrollmentMode = EnrollmentMode.Open);
            await contentManager.CreateAsync(course, VersionOptions.Draft);

            using var zip = new MemoryStream(Cmi5Package());
            var imported = await services.GetRequiredService<LearningPackageImporter>().ImportAsync("cmi5.zip", zip);

            Assert.True(imported.Succeeded, imported.Error);

            var item = await contentManager.NewAsync(PackagesConstants.ContentTypes.Package);

            item.DisplayText = "cmi5 package";
            item.Alter<TitlePart>(part => part.Title = "cmi5 package");
            item.Alter<LearningPackagePart>(part => imported.ApplyTo(part, "cmi5.zip", "admin", DateTime.UtcNow));
            item.Alter<CourseItemPart>(part => part.CompletionMode = CompletionModes.PackageReported);
            item.Alter<global::OrchardCore.Lists.Models.ContainedPart>(part =>
            {
                part.ListContentItemId = course.ContentItemId;
                part.ListContentType = course.ContentType;
            });
            await contentManager.CreateAsync(item, VersionOptions.Published);

            course.Alter<CourseOutlinePart>(part => part.Sections =
            [
                new CourseSection { Id = "s1", Title = "Lessons", Items = [new CourseOutlineItem { ContentItemId = item.ContentItemId }] },
            ]);
            await contentManager.UpdateAsync(course);
            await contentManager.PublishAsync(course);

            var enrolled = await services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
            {
                UserId = userId,
                CourseId = course.ContentItemId,
                Source = LearningConstants.EnrollmentSources.Manual,
                SourceId = "cmi5-test",
            });

            Assert.True(enrolled.Succeeded, enrolled.ErrorCode);

            return (course.ContentItemId, item.ContentItemId);
        });
    }
}
