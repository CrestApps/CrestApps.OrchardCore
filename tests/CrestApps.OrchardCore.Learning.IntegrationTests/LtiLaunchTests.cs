using System.Net;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Learning.Lti;
using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Models;
using OrchardCore.OpenId.Abstractions.Descriptors;
using OrchardCore.OpenId.Abstractions.Managers;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The launch of an External tool item on a running site, as an LTI 1.3 tool sees it: the learner's launch sends the
/// browser to the tool's login initiation URL with a signed message hint; the tool's login request to the platform's
/// authorization endpoint comes back as a page that posts an id token signed with the platform key published at
/// <c>/learning/lti/jwks</c>; forged hints, unknown redirect URIs and replayed nonces are refused.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed partial class LtiLaunchTests
{
    internal const string ClientId = "lti-launch-tool";
    internal const string LoginUrl = "https://tool.example.com/lti/login";
    internal const string RedirectUri = "https://tool.example.com/lti/launch";
    internal const string DeploymentId = "deployment-launch-1";

    internal static readonly RSA ToolKey = RSA.Create(2048);

    private static string _itemId;

    private readonly LearningSiteFixture _fixture;

    public LtiLaunchTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Launch_AsAnEnrolledLearner_SendsTheBrowserToTheToolsLoginInitiation()
    {
        // Arrange
        var itemId = await EnsureToolItemAsync();
        using var http = await _fixture.CreateClientAsync("learner1");

        // Act
        var response = await http.GetAsync($"learning/lti/launch/{itemId}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location;
        var query = QueryHelpers.ParseQuery(location.Query);

        Assert.Equal("tool.example.com", location.Host);
        Assert.Equal("/lti/login", location.AbsolutePath);
        Assert.Equal(_fixture.BaseAddress.AbsoluteUri.TrimEnd('/'), query["iss"]);
        Assert.Equal(ClientId, query["client_id"]);
        Assert.Equal(DeploymentId, query["lti_deployment_id"]);
        Assert.Equal("https://tool.example.com/lti/launch/forklift", query["target_link_uri"]);
        Assert.False(string.IsNullOrEmpty(query["login_hint"]));
        Assert.NotEqual(_fixture.EnrolledUserId, query["login_hint"].ToString());
        Assert.False(string.IsNullOrEmpty(query["lti_message_hint"]));
    }

    [Fact]
    public async Task Authorize_TheToolsLoginRequest_PostsAnIdTokenSignedWithThePublishedPlatformKey()
    {
        // Arrange
        var itemId = await EnsureToolItemAsync();
        using var http = await _fixture.CreateClientAsync("learner1");
        var login = await StartLoginAsync(http, itemId);

        // Act
        var response = await http.GetAsync("learning/lti/authorize" + AuthorizationQuery(login, "nonce-" + Guid.NewGuid().ToString("n"), RedirectUri), TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"action=\"{RedirectUri}\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"state\" value=\"state-1\"", html, StringComparison.Ordinal);

        var idToken = Field(html, "id_token");
        var jwks = await http.GetStringAsync("learning/lti/jwks", TestContext.Current.CancellationToken);
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuer = _fixture.BaseAddress.AbsoluteUri.TrimEnd('/'),
            ValidAudience = ClientId,
            IssuerSigningKeys = new JsonWebKeySet(jwks).GetSigningKeys(),
        });

        Assert.True(validation.IsValid, validation.Exception?.Message);

        var token = new JsonWebToken(idToken);
        Assert.Equal(LtiConstants.MessageTypes.ResourceLinkRequest, token.GetClaim(LtiConstants.Claims.MessageType).Value);
        Assert.Equal("1.3.0", token.GetClaim(LtiConstants.Claims.Version).Value);
        Assert.Equal(DeploymentId, token.GetClaim(LtiConstants.Claims.DeploymentId).Value);
        Assert.Equal(login["login_hint"], token.Subject);
        Assert.NotEqual(_fixture.EnrolledUserId, token.Subject);
        Assert.Equal(itemId, JsonNode.Parse(token.GetClaim(LtiConstants.Claims.ResourceLink).Value)?["id"]?.GetValue<string>());
        Assert.Equal(_fixture.CourseId, JsonNode.Parse(token.GetClaim(LtiConstants.Claims.Context).Value)?["id"]?.GetValue<string>());
        Assert.Contains(LtiConstants.Roles.Learner, token.GetPayloadValue<string[]>(LtiConstants.Claims.Roles));
        Assert.False(token.TryGetClaim("email", out _));
        Assert.False(token.TryGetClaim("name", out _));
    }

    [Fact]
    public async Task Authorize_AForgedMessageHint_IsPostedBackAsAnError()
    {
        // Arrange
        var itemId = await EnsureToolItemAsync();
        using var http = await _fixture.CreateClientAsync("learner1");
        var login = await StartLoginAsync(http, itemId);
        login["lti_message_hint"] = "forged-hint";

        // Act
        var html = await http.GetStringAsync("learning/lti/authorize" + AuthorizationQuery(login, "nonce-" + Guid.NewGuid().ToString("n"), RedirectUri), TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("name=\"id_token\"", html, StringComparison.Ordinal);
        Assert.Equal("invalid_request", Field(html, "error"));
    }

    [Fact]
    public async Task Authorize_ARedirectUriTheToolDidNotRegister_Answers400AndPostsNothing()
    {
        // Arrange
        var itemId = await EnsureToolItemAsync();
        using var http = await _fixture.CreateClientAsync("learner1");
        var login = await StartLoginAsync(http, itemId);

        // Act
        var response = await http.GetAsync("learning/lti/authorize" + AuthorizationQuery(login, "nonce-" + Guid.NewGuid().ToString("n"), "https://attacker.example.com/catch"), TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("attacker.example.com", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id_token", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorize_AReplayedNonce_IsRefused()
    {
        // Arrange
        var itemId = await EnsureToolItemAsync();
        using var http = await _fixture.CreateClientAsync("learner1");
        var nonce = "nonce-" + Guid.NewGuid().ToString("n");
        var first = await http.GetStringAsync("learning/lti/authorize" + AuthorizationQuery(await StartLoginAsync(http, itemId), nonce, RedirectUri), TestContext.Current.CancellationToken);

        // Act
        var replay = await http.GetStringAsync("learning/lti/authorize" + AuthorizationQuery(await StartLoginAsync(http, itemId), nonce, RedirectUri), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("name=\"id_token\"", first, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"id_token\"", replay, StringComparison.Ordinal);
        Assert.Equal("invalid_request", Field(replay, "error"));
    }

    [Fact]
    public async Task Launch_ALearnerWhoIsNotEnrolled_IsNotSentToTheTool()
    {
        // Arrange
        var itemId = await EnsureToolItemAsync();
        using var http = await _fixture.CreateClientAsync("learner2");

        // Act
        var response = await http.GetAsync($"learning/lti/launch/{itemId}", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task Authorize_ALoginRequestPostedWithoutTheCookie_IsSentBackAsAGetThatCarriesIt()
    {
        // Arrange: the sign-in cookie is SameSite=Lax, so a browser leaves it off a login request a tool posts from its
        // own site. The platform answers such a post with a redirect to the same request as a GET, which carries it.
        var itemId = await EnsureToolItemAsync();
        using var learner = await _fixture.CreateClientAsync("learner1");
        using var browserWithoutCookie = await _fixture.CreateClientAsync();
        var login = await StartLoginAsync(learner, itemId);
        var query = QueryHelpers.ParseQuery(AuthorizationQuery(login, "nonce-" + Guid.NewGuid().ToString("n"), RedirectUri))
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);

        // Act
        var posted = await browserWithoutCookie.PostAsync("learning/lti/authorize", new FormUrlEncodedContent(query), TestContext.Current.CancellationToken);
        var location = posted.Headers.Location;
        var followed = location is null ? null : await learner.GetStringAsync(location.ToString().TrimStart('/'), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.SeeOther, posted.StatusCode);
        Assert.NotNull(location);
        Assert.Equal(query, QueryHelpers.ParseQuery(location.IsAbsoluteUri ? location.Query : location.OriginalString[location.OriginalString.IndexOf('?')..])
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal));
        Assert.False(string.IsNullOrEmpty(Field(followed, "id_token")));
    }

    [Fact]
    public async Task Jwks_PublishesThePublicKeyOnly()
    {
        // Arrange
        using var http = await _fixture.CreateClientAsync();

        // Act
        var jwks = JsonNode.Parse(await http.GetStringAsync("learning/lti/jwks", TestContext.Current.CancellationToken));

        // Assert
        var key = Assert.Single(jwks!["keys"]!.AsArray());
        Assert.Equal("RSA", key!["kty"]?.GetValue<string>());
        Assert.Equal("RS256", key["alg"]?.GetValue<string>());
        Assert.False(string.IsNullOrEmpty(key["kid"]?.GetValue<string>()));
        Assert.Null(key["d"]);
        Assert.Null(key["p"]);
    }

    internal static async Task<string> EnsureToolAndItemAsync(LearningSiteFixture fixture, Action<ExternalTool> configure = null)
    {
        if (_itemId is not null)
        {
            return _itemId;
        }

        _itemId = await fixture.InTenantAsync(async services =>
        {
            var applications = services.GetRequiredService<IOpenIdApplicationManager>();

            if (await applications.FindByClientIdAsync(ClientId, TestContext.Current.CancellationToken) is null)
            {
                var key = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(ToolKey.ExportParameters(false)) { KeyId = "tool-key-1" });
                key.Use = "sig";
                key.Alg = SecurityAlgorithms.RsaSha256;

                var keySet = new JsonWebKeySet();
                keySet.Keys.Add(key);

                var descriptor = new OpenIdApplicationDescriptor
                {
                    ClientId = ClientId,
                    DisplayName = "Forklift simulator",
                    ClientType = OpenIddictConstants.ClientTypes.Confidential,
                    JsonWebKeySet = keySet,
                };

                descriptor.RedirectUris.Add(new Uri(RedirectUri));
                descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
                descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);

                foreach (var scope in LtiConstants.Scopes.All)
                {
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
                }

                await applications.CreateAsync(descriptor, TestContext.Current.CancellationToken);
            }

            var manager = services.GetRequiredService<IExternalToolManager>();
            var tool = await manager.NewAsync();

            tool.Name = "Forklift simulator";
            tool.ClientId = ClientId;
            tool.DeploymentId = DeploymentId;
            tool.LoginInitiationUrl = LoginUrl;
            tool.TargetLinkUri = RedirectUri;
            tool.DeepLinkingUrl = "https://tool.example.com/lti/deep-link";
            tool.AllowGrades = true;
            tool.AllowNamesAndRoles = true;
            tool.CustomParameters = "learner=$User.id";
            configure?.Invoke(tool);

            await manager.CreateAsync(tool);

            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await contentManager.GetAsync(fixture.CourseId, VersionOptions.Latest);
            var item = await contentManager.NewAsync(LtiConstants.ContentTypes.ExternalToolItem);

            item.DisplayText = "Forklift simulator";
            item.Alter<TitlePart>(part => part.Title = "Forklift simulator");
            item.Alter<ContainedPart>(part =>
            {
                part.ListContentItemId = course.ContentItemId;
                part.ListContentType = course.ContentType;
            });
            item.Alter<ExternalToolItemPart>(part =>
            {
                part.ToolId = tool.ItemId;
                part.TargetLinkUri = "https://tool.example.com/lti/launch/forklift";
                part.ScoreMaximum = 50;
            });
            item.Alter<CourseItemPart>(part =>
            {
                part.Kind = LtiConstants.ExternalToolKind;
                part.CompletionMode = CompletionModes.Graded;
                part.Gradable = true;
            });

            await contentManager.CreateAsync(item, VersionOptions.Published);

            course.Alter<CourseOutlinePart>(part => part.Sections[0].Items.Add(new CourseOutlineItem { ContentItemId = item.ContentItemId }));
            await contentManager.UpdateAsync(course);
            await contentManager.PublishAsync(course);

            return item.ContentItemId;
        });

        return _itemId;
    }

    internal static async Task<Dictionary<string, string>> StartLoginAsync(HttpClient http, string itemId)
    {
        var response = await http.GetAsync($"learning/lti/launch/{itemId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        return QueryHelpers.ParseQuery(response.Headers.Location.Query).ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);
    }

    internal static string AuthorizationQuery(IReadOnlyDictionary<string, string> login, string nonce, string redirectUri)
        => QueryString(new Dictionary<string, string>
        {
            ["scope"] = "openid",
            ["response_type"] = "id_token",
            ["response_mode"] = "form_post",
            ["prompt"] = "none",
            ["client_id"] = login["client_id"],
            ["redirect_uri"] = redirectUri,
            ["login_hint"] = login["login_hint"],
            ["lti_message_hint"] = login["lti_message_hint"],
            ["state"] = "state-1",
            ["nonce"] = nonce,
        });

    internal static string Field(string html, string name)
    {
        var match = Regex.Match(html, "name=\"" + Regex.Escape(name) + "\" value=\"(?<value>[^\"]*)\"");

        Assert.True(match.Success, $"The page has no {name} field: {html}");

        return WebUtility.HtmlDecode(match.Groups["value"].Value);
    }

    private Task<string> EnsureToolItemAsync()
        => EnsureToolAndItemAsync(_fixture);

    private static string QueryString(Dictionary<string, string> values)
        => "?" + string.Join("&", values.Select(pair => pair.Key + "=" + Uri.EscapeDataString(pair.Value)));
}
