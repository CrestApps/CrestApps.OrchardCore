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
/// SCORM 1.2 packages on a running site: the package files are served only under a launch token, with a sandbox
/// Content-Security-Policy that has no allow-same-origin, nosniff and their content types; pages get the run-time API;
/// the runtime endpoint takes values only from the token's learner with the session's antiforgery token; the import page
/// imports a package into a draft.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LearningPackagesTests
{
    private const string Manifest = """
        <?xml version="1.0" encoding="UTF-8"?>
        <manifest identifier="it.package" version="1.0" xmlns="http://www.imsproject.org/xsd/imscp_rootv1p1p2" xmlns:adlcp="http://www.adlnet.org/xsd/adlcp_rootv1p2">
          <metadata><schema>ADL SCORM</schema><schemaversion>1.2</schemaversion></metadata>
          <organizations default="O">
            <organization identifier="O">
              <title>Ladder Safety Package</title>
              <item identifier="I1" identifierref="R1"><title>Ladder lesson</title><adlcp:masteryscore>75</adlcp:masteryscore></item>
            </organization>
          </organizations>
          <resources>
            <resource identifier="R1" type="webcontent" adlcp:scormtype="sco" href="index.html"><file href="index.html"/></resource>
          </resources>
        </manifest>
        """;

    private readonly LearningSiteFixture _fixture;

    public LearningPackagesTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PackageFiles_AreServedSandboxedUnderTheLaunchTokenOnly()
    {
        // Arrange
        var (courseId, itemId) = await CreatePackageCourseAsync("learner1");
        using var learner = await _fixture.CreateClientAsync("learner1");
        var player = await learner.GetStringAsync($"learning/courses/{courseId}/play/{itemId}", TestContext.Current.CancellationToken);
        var launch = await LaunchAsync(learner, itemId, player);
        var launchUrl = launch["launchUrl"]!.GetValue<string>();
        var token = launch["token"]!.GetValue<string>();

        // Act
        var page = await learner.GetAsync(launchUrl.TrimStart('/'), TestContext.Current.CancellationToken);
        var pageHtml = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var script = await learner.GetAsync($"learning/packages/run/{token}/scripts/lesson.js", TestContext.Current.CancellationToken);

        using var anonymous = await _fixture.CreateClientAsync();
        var withoutCookie = await anonymous.GetAsync(launchUrl.TrimStart('/'), TestContext.Current.CancellationToken);
        var tampered = await anonymous.GetAsync($"learning/packages/run/{token[..^4]}AAAA/index.html", TestContext.Current.CancellationToken);
        var manifestOutside = await anonymous.GetAsync($"learning/packages/run/{token}/scripts/%2e%2e/%2e%2e/index.html", TestContext.Current.CancellationToken);
        var missing = await anonymous.GetAsync($"learning/packages/run/{token}/nothing-here.html", TestContext.Current.CancellationToken);

        // Assert: the player embeds the frame sandboxed, and the files carry the sandbox policy themselves.
        Assert.Contains("data-learning-package", player);
        Assert.DoesNotContain("allow-same-origin", player);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("text/html", page.Content.Headers.ContentType?.MediaType);
        AssertSandboxed(page);
        Assert.Equal("no-store", page.Headers.CacheControl?.ToString());
        Assert.Contains("window.CrestAppsScorm12Launch=", pageHtml);
        Assert.Contains("LMSInitialize", pageHtml);
        Assert.True(pageHtml.IndexOf("CrestAppsScorm12Launch", StringComparison.Ordinal) < pageHtml.IndexOf("<title>Ladder</title>", StringComparison.Ordinal));
        Assert.Contains("\"StudentName\":\"learner1\"", pageHtml);

        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Equal("text/javascript", script.Content.Headers.ContentType?.MediaType);
        AssertSandboxed(script);
        Assert.Equal("var lesson = 1;", await script.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // The token alone opens the files: the frame has no cookie.
        Assert.Equal(HttpStatusCode.OK, withoutCookie.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, tampered.StatusCode);
        Assert.Equal(PackageRunControllerPolicy, tampered.Headers.GetValues("Content-Security-Policy").Single());
        Assert.NotEqual(HttpStatusCode.OK, manifestOutside.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Runtime_TakesTheValuesOfTheTokensLearnerOnly_AndChecksThemOnTheServer()
    {
        // Arrange
        var (courseId, itemId) = await CreatePackageCourseAsync("learner1", "learner2");
        using var learner = await _fixture.CreateClientAsync("learner1");
        var player = await learner.GetStringAsync($"learning/courses/{courseId}/play/{itemId}", TestContext.Current.CancellationToken);
        var launch = await LaunchAsync(learner, itemId, player);
        var runtimeUrl = launch["runtimeUrl"]!.GetValue<string>().TrimStart('/');
        var token = launch["token"]!.GetValue<string>();

        using var other = await _fixture.CreateClientAsync("learner2");
        var otherPlayer = await other.GetStringAsync($"learning/courses/{courseId}/play/{itemId}", TestContext.Current.CancellationToken);

        // Act
        var accepted = await PostRuntimeAsync(learner, runtimeUrl, LearningSiteFixture.Token(player), token, """{"changes":[["cmi.core.lesson_location","slide-3"],["cmi.core.student_id","x"],["cmi.core.lesson_status","not attempted"]],"commit":true,"finish":false}""");
        var stolen = await PostRuntimeAsync(other, runtimeUrl, LearningSiteFixture.Token(otherPlayer), token, """{"changes":[["cmi.core.lesson_location","stolen"]],"commit":true,"finish":false}""");
        var withoutAntiforgery = await learner.PostAsync(runtimeUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = token,
            ["payload"] = """{"changes":[["cmi.core.lesson_location","csrf"]]}""",
        }), TestContext.Current.CancellationToken);

        var state = await _fixture.InTenantAsync(async services =>
        {
            var enrollment = (await services.GetRequiredService<IEnrollmentStore>().ListByCourseForUserAsync(_fixture.EnrolledUserId, courseId)).Single();

            return await services.GetRequiredService<PackageRuntimeStore>().FindAsync(enrollment.ItemId, itemId);
        });

        // Assert
        Assert.True(accepted["accepted"]!.GetValue<bool>());
        Assert.Equal(["403", "405"], accepted["refused"]!.AsArray().Select(refusal => refusal!["code"]!.GetValue<string>()));
        Assert.False(stolen["accepted"]!.GetValue<bool>());
        Assert.NotEqual(HttpStatusCode.OK, withoutAntiforgery.StatusCode);
        Assert.Equal("slide-3", state.Cmi.LessonLocation);
        Assert.Equal(_fixture.EnrolledUserId, state.Cmi.StudentId);
    }

    [Fact]
    public async Task ImportPage_ImportsAPackageIntoADraft_AndIsRefusedToALearner()
    {
        // Arrange
        var itemId = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var item = await contentManager.NewAsync(PackagesConstants.ContentTypes.Package);

            item.DisplayText = "Import target";
            item.Alter<TitlePart>(part => part.Title = "Import target");
            await contentManager.CreateAsync(item, VersionOptions.Published);

            return item.ContentItemId;
        });

        using var admin = await _fixture.CreateClientAsync("admin");
        var form = await admin.GetAsync($"Admin/learning/packages/{itemId}/upload", TestContext.Current.CancellationToken);
        var formHtml = await form.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var editor = await admin.GetAsync($"Admin/Contents/ContentItems/{itemId}/Edit", TestContext.Current.CancellationToken);

        // Act
        using var upload = new MultipartFormDataContent
        {
            { new StringContent(LearningSiteFixture.Token(formHtml)), "__RequestVerificationToken" },
        };
        var file = new ByteArrayContent(Package());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        upload.Add(file, "package", "ladder.zip");
        var imported = await admin.PostAsync($"Admin/learning/packages/{itemId}/import", upload, TestContext.Current.CancellationToken);

        using var learner = await _fixture.CreateClientAsync("learner1");
        var refused = await learner.GetAsync($"Admin/learning/packages/{itemId}/upload", TestContext.Current.CancellationToken);

        var (draft, published) = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();

            return (PartOf(await contentManager.GetAsync(itemId, VersionOptions.Draft)), PartOf(await contentManager.GetAsync(itemId, VersionOptions.Published)));
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        Assert.Contains("action-bar-bottom", formHtml);
        Assert.Equal(HttpStatusCode.OK, editor.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, imported.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, refused.StatusCode);
        Assert.Equal("Ladder Safety Package", draft.Title);
        Assert.Equal("index.html", draft.LaunchPath);
        Assert.Equal(75m, draft.MasteryScore);
        Assert.False(string.IsNullOrEmpty(draft.PackageFileId));
        Assert.True(string.IsNullOrEmpty(published?.PackageFileId));
    }

    private static LearningPackagePart PartOf(ContentItem item)
        => item is not null && item.TryGet<LearningPackagePart>(out var part) ? part : null;

    private const string PackageRunControllerPolicy = "sandbox allow-scripts allow-forms allow-popups";

    private static void AssertSandboxed(HttpResponseMessage response)
    {
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();

        Assert.Equal(PackageRunControllerPolicy, policy);
        Assert.DoesNotContain("allow-same-origin", policy);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    }

    private static async Task<JsonObject> LaunchAsync(HttpClient client, string itemId, string playerHtml)
    {
        var response = await client.PostAsync($"learning/packages/{itemId}/launch", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(playerHtml),
        }), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject();

        Assert.True(result["launched"]!.GetValue<bool>(), result["message"]?.GetValue<string>());

        return result;
    }

    private static async Task<JsonObject> PostRuntimeAsync(HttpClient client, string runtimeUrl, string antiforgery, string token, string payload)
    {
        var response = await client.PostAsync(runtimeUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgery,
            ["token"] = token,
            ["payload"] = payload,
        }), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject();
    }

    private static byte[] Package()
    {
        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, "imsmanifest.xml", Manifest.TrimStart());
            Add(archive, "index.html", "<!DOCTYPE html><html><head><title>Ladder</title><script src=\"scripts/lesson.js\"></script></head><body>Ladder lesson</body></html>");
            Add(archive, "scripts/lesson.js", "var lesson = 1;");
        }

        return stream.ToArray();
    }

    private static void Add(ZipArchive archive, string name, string content)
    {
        using var writer = archive.CreateEntry(name).Open();
        writer.Write(Encoding.UTF8.GetBytes(content));
    }

    // A course of its own with one package item, so the shared course of the fixture keeps its outline.
    private async Task<(string CourseId, string ItemId)> CreatePackageCourseAsync(params string[] learners)
    {
        var userIds = new List<string>();

        foreach (var learner in learners)
        {
            userIds.Add(learner == "learner1" ? _fixture.EnrolledUserId : await _fixture.InTenantAsync(services => UserIdAsync(services, learner)));
        }

        return await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);
            var title = "Package course " + Guid.NewGuid().ToString("n")[..6];

            course.DisplayText = title;
            course.Alter<TitlePart>(part => part.Title = title);
            course.Alter<CoursePart>(part => part.EnrollmentMode = EnrollmentMode.Open);
            await contentManager.CreateAsync(course, VersionOptions.Draft);

            using var zip = new MemoryStream(Package());
            var imported = await services.GetRequiredService<LearningPackageImporter>().ImportAsync("ladder.zip", zip);

            Assert.True(imported.Succeeded, imported.Error);

            var item = await contentManager.NewAsync(PackagesConstants.ContentTypes.Package);

            item.DisplayText = "Ladder package";
            item.Alter<TitlePart>(part => part.Title = "Ladder package");
            item.Alter<LearningPackagePart>(part => imported.ApplyTo(part, "ladder.zip", "admin", DateTime.UtcNow));

            // Written back so the kind and completion mode the type gives a new item are kept, as the editor does.
            item.Alter<CourseItemPart>(part => part.CompletionMode = CompletionModes.PackageReported);
            item.Alter<global::OrchardCore.Lists.Models.ContainedPart>(part =>
            {
                part.ListContentItemId = course.ContentItemId;
                part.ListContentType = course.ContentType;
            });
            await contentManager.CreateAsync(item, VersionOptions.Published);

            course.Alter<CourseOutlinePart>(part => part.Sections =
            [
                new CourseSection { Id = "s1", Title = "Package", Items = [new CourseOutlineItem { ContentItemId = item.ContentItemId }] },
            ]);
            await contentManager.UpdateAsync(course);
            await contentManager.PublishAsync(course);

            foreach (var userId in userIds)
            {
                var enrolled = await services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
                {
                    UserId = userId,
                    CourseId = course.ContentItemId,
                    Source = LearningConstants.EnrollmentSources.Manual,
                    SourceId = "packages-test",
                });

                Assert.True(enrolled.Succeeded, enrolled.ErrorCode);
            }

            return (course.ContentItemId, item.ContentItemId);
        });
    }

    private static async Task<string> UserIdAsync(IServiceProvider services, string userName)
    {
        var user = await services.GetRequiredService<global::OrchardCore.Users.Services.IUserService>().GetUserAsync(userName) as global::OrchardCore.Users.Models.User;

        return user!.UserId;
    }
}
