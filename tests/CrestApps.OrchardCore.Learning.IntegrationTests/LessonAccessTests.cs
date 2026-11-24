using System.Net;
using System.Net.Http.Json;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// A person who is not enrolled cannot open a lesson, its attached file or another learner's progress, by the display
/// route, the player, the media URL, the content API or GraphQL; the enrolled learner can.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LessonAccessTests
{
    private readonly LearningSiteFixture _fixture;

    public LessonAccessTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TheLessonFile_IsServedToTheEnrolledLearnerOnly()
    {
        using var anonymous = await _fixture.CreateClientAsync();
        using var outsider = await _fixture.CreateClientAsync("learner2");
        using var learner = await _fixture.CreateClientAsync("learner1");
        using var admin = await _fixture.CreateClientAsync("admin");

        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(anonymous, _fixture.LessonFilePath)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(outsider, _fixture.LessonFilePath)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(learner, _fixture.LessonFilePath)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(admin, _fixture.LessonFilePath)).StatusCode);
    }

    [Fact]
    public async Task TheLessonDisplayRoute_IsRefusedToPeopleWhoAreNotEnrolled()
    {
        using var anonymous = await _fixture.CreateClientAsync();
        using var outsider = await _fixture.CreateClientAsync("learner2");
        using var learner = await _fixture.CreateClientAsync("learner1");
        var path = $"Contents/ContentItems/{_fixture.PageLessonId}";

        var anonymousResponse = await GetAsync(anonymous, path);
        var outsiderResponse = await GetAsync(outsider, path);
        var learnerResponse = await GetAsync(learner, path);

        Assert.NotEqual(HttpStatusCode.OK, anonymousResponse.StatusCode);
        Assert.DoesNotContain(LearningSiteFixture.SecretLessonText, await anonymousResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.NotEqual(HttpStatusCode.OK, outsiderResponse.StatusCode);
        Assert.DoesNotContain(LearningSiteFixture.SecretLessonText, await outsiderResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.OK, learnerResponse.StatusCode);
        Assert.Contains(LearningSiteFixture.SecretLessonText, await learnerResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ThePlayer_ShowsTheLessonToTheEnrolledLearner_AndOnlyTheEnrollButtonToOthers()
    {
        using var outsider = await _fixture.CreateClientAsync("learner2");
        using var learner = await _fixture.CreateClientAsync("learner1");
        var path = $"learning/courses/{_fixture.CourseId}/play/{_fixture.PageLessonId}";

        var outsiderPage = await (await GetAsync(outsider, path)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var learnerResponse = await GetAsync(learner, path);

        Assert.DoesNotContain(LearningSiteFixture.SecretLessonText, outsiderPage);
        Assert.Equal(HttpStatusCode.OK, learnerResponse.StatusCode);
        Assert.Contains(LearningSiteFixture.SecretLessonText, await learnerResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheCoursePage_IsAtCoursesSlash_AndShowsTheOutlineWithoutTheLessonBody()
    {
        using var anonymous = await _fixture.CreateClientAsync();

        var response = await GetAsync(anonymous, "courses/workplace-safety");
        var page = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Fire exits", page);
        Assert.DoesNotContain(LearningSiteFixture.SecretLessonText, page);
    }

    [Fact]
    public async Task TheContentApi_AndGraphQL_DoNotReturnTheLessonToPeopleWhoAreNotEnrolled()
    {
        using var anonymous = await _fixture.CreateClientAsync();

        var api = await GetAsync(anonymous, $"api/content/{_fixture.PageLessonId}");
        Assert.NotEqual(HttpStatusCode.OK, api.StatusCode);
        Assert.DoesNotContain(LearningSiteFixture.SecretLessonText, await api.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var graphQL = await anonymous.PostAsJsonAsync("api/graphql", new
        {
            query = "{ pageLesson { contentItemId displayText htmlBody { html } } }",
        }, TestContext.Current.CancellationToken);
        var body = await graphQL.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(LearningSiteFixture.SecretLessonText, body);
        Assert.DoesNotContain(_fixture.PageLessonId, body);
    }

    [Fact]
    public async Task AnotherLearnersProgress_AndTheAdminEnrollment_AreRefused()
    {
        using var outsider = await _fixture.CreateClientAsync("learner2");
        var page = await (await GetAsync(outsider, "courses/workplace-safety")).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var token = LearningSiteFixture.Token(page);

        var progress = await outsider.PostAsync($"learning/progress/{_fixture.EnrollmentId}/{_fixture.PageLessonId}/done", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        }), TestContext.Current.CancellationToken);
        var detail = await GetAsync(outsider, $"Admin/learning/enrollments/{_fixture.EnrollmentId}");

        Assert.Equal(HttpStatusCode.NotFound, progress.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, detail.StatusCode);
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string path)
        => client.GetAsync(path, TestContext.Current.CancellationToken);
}
