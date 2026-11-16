using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Lti;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The LTI Advantage services on a running site: a tool gets an access token at the OpenID server's token endpoint with
/// a client assertion in the shape LTI tools send it (typ JWT, aud the token endpoint), reads the course roster (Names
/// and Role Provisioning Services 2.0) and manages a line item and posts a learner's score (Assignment and Grade Services
/// 2.0), and the fully graded score reaches the learner's progress as a server-to-server result.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LtiServicesTests
{
    private readonly LearningSiteFixture _fixture;

    public LtiServicesTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TokenEndpoint_AToolsLtiShapedClientAssertion_GetsAnAccessToken_AnotherKeyOrAReplayDoesNot()
    {
        // Arrange
        await LtiLaunchTests.EnsureToolAndItemAsync(_fixture);
        using var http = new HttpClient { BaseAddress = _fixture.BaseAddress };
        using var otherKey = RSA.Create(2048);
        var jti = Guid.NewGuid().ToString("n");

        // Act
        var accepted = await http.PostAsync("connect/token", TokenRequest(Assertion(LtiLaunchTests.ToolKey, jti), LtiConstants.Scopes.Score), TestContext.Current.CancellationToken);
        var replayed = await http.PostAsync("connect/token", TokenRequest(Assertion(LtiLaunchTests.ToolKey, jti), LtiConstants.Scopes.Score), TestContext.Current.CancellationToken);
        var forged = await http.PostAsync("connect/token", TokenRequest(Assertion(otherKey, Guid.NewGuid().ToString("n")), LtiConstants.Scopes.Score), TestContext.Current.CancellationToken);

        // Assert
        var body = await accepted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(accepted.StatusCode == HttpStatusCode.OK, body);
        Assert.False(string.IsNullOrEmpty(JsonNode.Parse(body)?["access_token"]?.GetValue<string>()));
        Assert.Equal(HttpStatusCode.Unauthorized, replayed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);
    }

    [Fact]
    public async Task Memberships_ListTheLearnersAndTheStaff_WithTheIdsMadeForTheTool()
    {
        // Arrange
        await LtiLaunchTests.EnsureToolAndItemAsync(_fixture);
        using var http = await ServiceClientAsync(LtiConstants.Scopes.ContextMembershipReadOnly);
        var learner = await SubjectAsync(_fixture.EnrolledUserId);
        var instructor = await SubjectAsync(_fixture.InstructorId);

        // Act
        var response = await http.GetAsync($"learning/lti/nrps/{_fixture.CourseId}/memberships", TestContext.Current.CancellationToken);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(LtiConstants.MediaTypes.MembershipContainer, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(_fixture.CourseId, body["context"]?["id"]?.GetValue<string>());

        var members = body["members"]!.AsArray();
        var learnerEntry = Assert.Single(members, member => member?["user_id"]?.GetValue<string>() == learner);
        var instructorEntry = Assert.Single(members, member => member?["user_id"]?.GetValue<string>() == instructor);

        Assert.Equal("Active", learnerEntry!["status"]?.GetValue<string>());
        Assert.Contains(LtiConstants.Roles.Learner, learnerEntry["roles"]!.AsArray().Select(role => role!.GetValue<string>()));
        Assert.Contains(LtiConstants.Roles.Instructor, instructorEntry!["roles"]!.AsArray().Select(role => role!.GetValue<string>()));
        Assert.DoesNotContain(_fixture.EnrolledUserId, body.ToJsonString(), StringComparison.Ordinal);
        Assert.Null(learnerEntry["email"]);
    }

    [Fact]
    public async Task Memberships_PagedByOne_LinkToTheNextPage()
    {
        // Arrange
        await LtiLaunchTests.EnsureToolAndItemAsync(_fixture);
        using var http = await ServiceClientAsync(LtiConstants.Scopes.ContextMembershipReadOnly);

        // Act
        var response = await http.GetAsync($"learning/lti/nrps/{_fixture.CourseId}/memberships?limit=1", TestContext.Current.CancellationToken);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;

        // Assert
        Assert.Single(body["members"]!.AsArray());
        Assert.Contains("rel=\"next\"", string.Join(",", response.Headers.GetValues("Link")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scores_AFullyGradedScore_ReachesTheLearnersProgress_AndTheResults()
    {
        // Arrange
        var itemId = await LtiLaunchTests.EnsureToolAndItemAsync(_fixture);
        using var http = await ServiceClientAsync(string.Join(' ', LtiConstants.Scopes.LineItem, LtiConstants.Scopes.ResultReadOnly, LtiConstants.Scopes.Score));
        var learner = await SubjectAsync(_fixture.EnrolledUserId);
        var created = await http.PostAsync($"learning/lti/ags/{_fixture.CourseId}/lineitems", Json(new JsonObject
        {
            ["scoreMaximum"] = 50,
            ["label"] = "Forklift simulator",
            ["resourceLinkId"] = itemId,
            ["tag"] = "simulator",
        }, LtiConstants.MediaTypes.LineItem), TestContext.Current.CancellationToken);
        var lineItem = JsonNode.Parse(await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
        var lineItemUrl = lineItem["id"]!.GetValue<string>();

        // Act
        var scored = await http.PostAsync(lineItemUrl + "/scores", Json(Score(learner, 40, "2030-01-01T10:00:00Z"), LtiConstants.MediaTypes.Score), TestContext.Current.CancellationToken);

        // A request's session commits when its scope ends, which can be after its response arrived.
        var results = new JsonArray();

        for (var attempt = 0; attempt < 50 && results.Count == 0; attempt++)
        {
            results = JsonNode.Parse(await http.GetStringAsync(lineItemUrl + "/results?user_id=" + Uri.EscapeDataString(learner), TestContext.Current.CancellationToken))!.AsArray();

            if (results.Count == 0)
            {
                await Task.Delay(100, TestContext.Current.CancellationToken);
            }
        }

        var earlier = await http.PostAsync(lineItemUrl + "/scores", Json(Score(learner, 10, "2029-01-01T10:00:00Z"), LtiConstants.MediaTypes.Score), TestContext.Current.CancellationToken);
        var stranger = await http.PostAsync(lineItemUrl + "/scores", Json(Score("not-a-member", 10, "2030-01-02T10:00:00Z"), LtiConstants.MediaTypes.Score), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(itemId, lineItem["resourceLinkId"]?.GetValue<string>());
        Assert.Equal(HttpStatusCode.NoContent, scored.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, earlier.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, stranger.StatusCode);

        var result = Assert.Single(results);
        Assert.Equal(learner, result!["userId"]?.GetValue<string>());
        Assert.Equal(40, result["resultScore"]?.GetValue<decimal>());
        Assert.Equal(50, result["resultMaximum"]?.GetValue<decimal>());

        CourseItemProgress item = null;
        await EventuallyAsync(async () => (item = (await _fixture.InTenantAsync(services => services.GetRequiredService<ILearnerProgressService>().GetAsync(_fixture.EnrollmentId, TestContext.Current.CancellationToken))).Find(itemId))?.Score is not null);

        Assert.NotNull(item);
        Assert.Equal(80, item.Score);
        Assert.Equal(CourseItemState.Completed, item.State);
        Assert.False(item.ResultSelfReported);
    }

    [Fact]
    public async Task LineItems_CanBeListedReadChangedAndRemoved_ByTheirTool()
    {
        // Arrange
        await LtiLaunchTests.EnsureToolAndItemAsync(_fixture);
        using var http = await ServiceClientAsync(LtiConstants.Scopes.LineItem);
        var created = JsonNode.Parse(await (await http.PostAsync($"learning/lti/ags/{_fixture.CourseId}/lineitems", Json(new JsonObject
        {
            ["scoreMaximum"] = 10,
            ["label"] = "Quiz",
            ["tag"] = "crud-" + Guid.NewGuid().ToString("n"),
        }, LtiConstants.MediaTypes.LineItem), TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
        var url = created["id"]!.GetValue<string>();
        var tag = created["tag"]!.GetValue<string>();

        // Act: each step waits for the one before to commit, which can be after its response arrived.
        JsonArray listed = null;
        await EventuallyAsync(async () => (listed = JsonNode.Parse(await http.GetStringAsync($"learning/lti/ags/{_fixture.CourseId}/lineitems?tag={tag}", TestContext.Current.CancellationToken))!.AsArray()).Count > 0);
        var updated = await http.PutAsync(url, Json(new JsonObject { ["scoreMaximum"] = 20, ["label"] = "Quiz, revised", ["tag"] = tag }, LtiConstants.MediaTypes.LineItem), TestContext.Current.CancellationToken);
        JsonNode read = null;
        await EventuallyAsync(async () => (read = JsonNode.Parse(await http.GetStringAsync(url, TestContext.Current.CancellationToken))!)["scoreMaximum"]?.GetValue<decimal>() == 20);
        var deleted = await http.DeleteAsync(url, TestContext.Current.CancellationToken);
        HttpResponseMessage gone = null;
        await EventuallyAsync(async () => (gone = await http.GetAsync(url, TestContext.Current.CancellationToken)).StatusCode == HttpStatusCode.NotFound);

        // Assert
        Assert.Equal(url, Assert.Single(listed)!["id"]?.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(20, read["scoreMaximum"]?.GetValue<decimal>());
        Assert.Equal("Quiz, revised", read["label"]?.GetValue<string>());
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task Services_RefuseATokenWithoutTheScope_AndTheSignInCookie()
    {
        // Arrange
        await LtiLaunchTests.EnsureToolAndItemAsync(_fixture);
        using var rosterOnly = await ServiceClientAsync(LtiConstants.Scopes.ContextMembershipReadOnly);
        using var admin = await _fixture.CreateClientAsync("admin");

        // Act
        var noScope = await rosterOnly.PostAsync($"learning/lti/ags/{_fixture.CourseId}/lineitems", Json(new JsonObject { ["scoreMaximum"] = 1, ["label"] = "x" }, LtiConstants.MediaTypes.LineItem), TestContext.Current.CancellationToken);
        var cookie = await admin.GetAsync($"learning/lti/nrps/{_fixture.CourseId}/memberships", TestContext.Current.CancellationToken);
        var otherCourse = await rosterOnly.GetAsync($"learning/lti/nrps/{_fixture.OtherCourseId}/memberships", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, noScope.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, cookie.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherCourse.StatusCode);
    }

    private static async Task EventuallyAsync(Func<Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    private async Task<HttpClient> ServiceClientAsync(string scope)
    {
        var http = new HttpClient { BaseAddress = _fixture.BaseAddress };
        var response = await http.PostAsync("connect/token", TokenRequest(Assertion(LtiLaunchTests.ToolKey, Guid.NewGuid().ToString("n")), scope), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JsonNode.Parse(body)!["access_token"]!.GetValue<string>());

        return http;
    }

    private Task<string> SubjectAsync(string userId)
        => _fixture.InTenantAsync(async services =>
            LtiSubjects.For(await services.GetRequiredService<IExternalToolStore>().FindByClientIdAsync(LtiLaunchTests.ClientId, TestContext.Current.CancellationToken), userId));

    private string Assertion(RSA key, string jti)
    {
        var now = DateTime.UtcNow;

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = LtiLaunchTests.ClientId,
            Audience = new Uri(_fixture.BaseAddress, "connect/token").AbsoluteUri,
            Claims = new Dictionary<string, object> { ["sub"] = LtiLaunchTests.ClientId, ["jti"] = jti },
            IssuedAt = now,
            Expires = now.AddMinutes(5),
            TokenType = "JWT",
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(key) { KeyId = "tool-key-1" }, SecurityAlgorithms.RsaSha256),
        });
    }

    private static FormUrlEncodedContent TokenRequest(string assertion, string scope)
        => new(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            ["client_assertion"] = assertion,
            ["scope"] = scope,
        });

    private static JsonObject Score(string userId, decimal given, string timestamp)
        => new()
        {
            ["userId"] = userId,
            ["scoreGiven"] = given,
            ["scoreMaximum"] = 50,
            ["activityProgress"] = "Completed",
            ["gradingProgress"] = "FullyGraded",
            ["timestamp"] = timestamp,
        };

    private static StringContent Json(JsonObject body, string mediaType)
        => new(body.ToJsonString(), Encoding.UTF8, mediaType);
}
