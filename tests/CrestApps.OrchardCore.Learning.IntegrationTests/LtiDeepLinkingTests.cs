using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Lti;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Deep Linking 2.0 on a running site: a course manager starts a Deep Linking request from the course, the id token
/// carries the Deep Linking settings with the signed data, and the tool's response, posted without the sign-in cookie,
/// adds its <c>ltiResourceLink</c> items to the course outline as External tool items; a response signed by another key
/// or answering the same request twice is refused.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LtiDeepLinkingTests
{
    private readonly LearningSiteFixture _fixture;

    public LtiDeepLinkingTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DeepLinking_TheToolsResponse_AddsItsResourceLinksToTheCourse()
    {
        // Arrange
        var settings = await StartAsync();
        using var tool = new HttpClient { BaseAddress = _fixture.BaseAddress };

        // Act
        var response = await tool.PostAsync("learning/lti/deep-linking/return", Form(Response(settings, LtiLaunchTests.ToolKey, new JsonArray(
            new JsonObject
            {
                ["type"] = "ltiResourceLink",
                ["title"] = "Pallet stacking drill",
                ["text"] = "Stack three pallets safely.",
                ["url"] = "https://tool.example.com/lti/launch/pallets",
                ["lineItem"] = new JsonObject { ["scoreMaximum"] = 20, ["label"] = "Pallet drill" },
                ["custom"] = new JsonObject { ["drill"] = "pallets" },
            },
            new JsonObject { ["type"] = "link", ["url"] = "https://www.example.com/video" }))), TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(response.StatusCode == HttpStatusCode.OK, html);
        Assert.Contains("Pallet stacking drill", html, StringComparison.Ordinal);
        Assert.Contains("link", html, StringComparison.Ordinal);

        // The request's session is committed when its scope ends, which can be after the response body arrived.
        ContentItem picked = null;
        string draftVersion = null;
        string outline = null;

        for (var attempt = 0; attempt < 50 && picked is null; attempt++)
        {
            (picked, draftVersion, outline) = await _fixture.InTenantAsync(async services =>
            {
                var contentManager = services.GetRequiredService<IContentManager>();
                var course = await contentManager.GetAsync(_fixture.CourseId, VersionOptions.Latest);
                var ids = course.Get<CourseOutlinePart>(nameof(CourseOutlinePart)).Sections.SelectMany(section => section.Items).Select(item => item.ContentItemId).ToArray();
                var items = await contentManager.GetAsync(ids, VersionOptions.Published);

                return (
                    items.SingleOrDefault(item => item.DisplayText == "Pallet stacking drill"),
                    course.Published ? null : course.ContentItemVersionId,
                    $"published={course.Published}, outline=[{string.Join(", ", items.Select(item => item.DisplayText))}]");
            });

            if (picked is null)
            {
                await Task.Delay(100, TestContext.Current.CancellationToken);
            }
        }

        Assert.True(picked is not null, $"{outline}; response: {html}");
        Assert.NotNull(draftVersion);

        var part = picked.Get<ExternalToolItemPart>(nameof(ExternalToolItemPart));

        Assert.Equal(LtiConstants.ContentTypes.ExternalToolItem, picked.ContentType);
        Assert.Equal("https://tool.example.com/lti/launch/pallets", part.TargetLinkUri);
        Assert.Equal(20, part.ScoreMaximum);
        Assert.Equal("Pallet drill", part.LineItemLabel);
        Assert.Equal("drill=pallets", part.CustomParameters);
        Assert.True(picked.Get<CourseItemPart>(nameof(CourseItemPart)).Gradable);

        await DiscardCourseDraftAsync("Pallet stacking drill");
    }

    [Fact]
    public async Task DeepLinking_AResponseSignedByAnotherKey_IsRefused()
    {
        // Arrange
        var settings = await StartAsync();
        using var tool = new HttpClient { BaseAddress = _fixture.BaseAddress };
        using var otherKey = RSA.Create(2048);

        // Act
        var response = await tool.PostAsync("learning/lti/deep-linking/return", Form(Response(settings, otherKey, new JsonArray(new JsonObject { ["type"] = "ltiResourceLink", ["title"] = "Forged" }))), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeepLinking_TheSameRequestAnsweredTwice_IsRefusedTheSecondTime()
    {
        // Arrange
        var settings = await StartAsync();
        using var tool = new HttpClient { BaseAddress = _fixture.BaseAddress };
        var items = new JsonArray(new JsonObject { ["type"] = "ltiResourceLink", ["title"] = "Once only" });
        var first = await tool.PostAsync("learning/lti/deep-linking/return", Form(Response(settings, LtiLaunchTests.ToolKey, items)), TestContext.Current.CancellationToken);

        // Act
        var second = await tool.PostAsync("learning/lti/deep-linking/return", Form(Response(settings, LtiLaunchTests.ToolKey, items)), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        await DiscardCourseDraftAsync("Once only");
    }

    // Picked items go into a draft of the shared course. The draft is discarded so the other tests of the collection,
    // which edit the course with DraftRequired, start from the published course: left in place, the full run lost the
    // published course (the course page answered 404 and launches failed).
    private async Task DiscardCourseDraftAsync(string pickedTitle)
    {
        // The request's session is committed when its scope ends, which can be after the response body arrived, so the
        // draft is discarded only once it holds the picked item; discarding earlier raced the request's own commit.
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var discarded = await _fixture.InTenantAsync(async services =>
            {
                var contentManager = services.GetRequiredService<IContentManager>();
                var latest = await contentManager.GetAsync(_fixture.CourseId, VersionOptions.Latest);

                if (latest.Published)
                {
                    return false;
                }

                var ids = latest.Get<CourseOutlinePart>(nameof(CourseOutlinePart)).Sections.SelectMany(section => section.Items).Select(item => item.ContentItemId).ToArray();

                if (!(await contentManager.GetAsync(ids, VersionOptions.Latest)).Any(item => item.DisplayText == pickedTitle))
                {
                    return false;
                }

                await contentManager.DiscardDraftAsync(latest);

                return true;
            });

            if (discarded)
            {
                return;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    // Starts a Deep Linking request as the site's administrator and returns the settings claim of its id token.
    private async Task<JsonObject> StartAsync()
    {
        await LtiLaunchTests.EnsureToolAndItemAsync(_fixture);

        var toolId = await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<CrestApps.OrchardCore.Learning.Lti.Core.Services.IExternalToolStore>().FindByClientIdAsync(LtiLaunchTests.ClientId, TestContext.Current.CancellationToken)).ItemId);

        using var http = await _fixture.CreateClientAsync("admin");
        var start = await http.GetAsync($"learning/lti/deep-linking/{_fixture.CourseId}/{toolId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);

        var login = QueryHelpers.ParseQuery(start.Headers.Location.Query).ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);

        Assert.Equal("https://tool.example.com/lti/deep-link", login["target_link_uri"]);

        var html = await http.GetStringAsync("learning/lti/authorize" + LtiLaunchTests.AuthorizationQuery(login, "nonce-" + Guid.NewGuid().ToString("n"), LtiLaunchTests.RedirectUri), TestContext.Current.CancellationToken);
        var token = new JsonWebToken(LtiLaunchTests.Field(html, "id_token"));

        Assert.Equal(LtiConstants.MessageTypes.DeepLinkingRequest, token.GetClaim(LtiConstants.Claims.MessageType).Value);
        Assert.Contains(LtiConstants.Roles.Instructor, token.GetPayloadValue<string[]>(LtiConstants.Claims.Roles));

        return JsonNode.Parse(token.GetClaim(LtiConstants.Claims.DeepLinkingSettings).Value)!.AsObject();
    }

    private string Response(JsonObject settings, RSA key, JsonArray items)
    {
        var now = DateTime.UtcNow;

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = LtiLaunchTests.ClientId,
            Audience = _fixture.BaseAddress.AbsoluteUri.TrimEnd('/'),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(5),
            Claims = new Dictionary<string, object>
            {
                ["nonce"] = Guid.NewGuid().ToString("n"),
                [LtiConstants.Claims.MessageType] = LtiConstants.MessageTypes.DeepLinkingResponse,
                [LtiConstants.Claims.Version] = "1.3.0",
                [LtiConstants.Claims.DeploymentId] = LtiLaunchTests.DeploymentId,
                [LtiConstants.Claims.Data] = settings["data"]!.GetValue<string>(),
                [LtiConstants.Claims.ContentItems] = JsonSerializer.SerializeToElement(items),
            },
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(key) { KeyId = "tool-key-1" }, SecurityAlgorithms.RsaSha256),
        });
    }

    private static FormUrlEncodedContent Form(string jwt)
        => new(new Dictionary<string, string> { ["JWT"] = jwt });
}
