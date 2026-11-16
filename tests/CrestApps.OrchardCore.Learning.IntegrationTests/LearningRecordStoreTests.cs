using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OrchardCore.OpenId.Abstractions.Descriptors;
using OrchardCore.OpenId.Abstractions.Managers;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The Learning Record Store on a running site: an OpenID application whose role holds the LRS permissions reaches it
/// with HTTP Basic; the sign-in cookie alone never does; every response carries the version header and the CORS headers,
/// preflights are answered for any origin without credentials, and the alternate request syntax is unwrapped.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LearningRecordStoreTests
{
    private const string ClientId = "lrs-integration";
    private const string Secret = LearningSiteFixture.Password;

    private static readonly SemaphoreSlim _clientLock = new(1, 1);
    private static bool _clientCreated;

    private readonly LearningSiteFixture _fixture;

    public LearningRecordStoreTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task OpenIdClient_WithBasic_StoresAndReadsAStatement_WithTheVersionAndCorsHeaders()
    {
        // Arrange
        await EnsureClientAsync();
        using var http = Client(ClientId, Secret);
        var id = Guid.NewGuid().ToString();

        // Act
        var put = await http.PutAsync($"learning/xapi/statements?statementId={id}", Json(Statement()), TestContext.Current.CancellationToken);
        var get = await http.GetAsync($"learning/xapi/statements?statementId={id}", TestContext.Current.CancellationToken);
        var body = JsonNode.Parse(await get.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal("1.0.3", Header(put, XapiConstants.Headers.Version));
        Assert.Equal("1.0.3", Header(get, XapiConstants.Headers.Version));
        Assert.Equal("*", Header(get, "Access-Control-Allow-Origin"));
        Assert.Null(Header(get, "Access-Control-Allow-Credentials"));
        Assert.Equal(ClientId, body!["authority"]!["account"]!["name"]!.GetValue<string>());
        Assert.NotNull(Header(get, XapiConstants.Headers.ConsistentThrough));
    }

    [Fact]
    public async Task TheSignInCookie_Alone_Gets401()
    {
        // Arrange: the site's administrator, signed in, whose role holds every LRS permission.
        using var admin = await _fixture.CreateClientAsync("admin");

        admin.DefaultRequestHeaders.Add(XapiConstants.Headers.Version, "1.0.3");

        // Act
        var read = await admin.GetAsync("learning/xapi/statements", TestContext.Current.CancellationToken);
        var write = await admin.PostAsync("learning/xapi/statements", Json(Statement()), TestContext.Current.CancellationToken);
        var adminPage = await admin.GetAsync("Admin/learning/statements", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
        Assert.Equal("1.0.3", Header(read, XapiConstants.Headers.Version));
        Assert.Equal(HttpStatusCode.OK, adminPage.StatusCode);
    }

    [Fact]
    public async Task WrongSecret_Gets401_AndAboutNeedsNoCredential()
    {
        // Arrange
        await EnsureClientAsync();
        using var wrong = Client(ClientId, "not-the-secret");
        using var anonymous = new HttpClient { BaseAddress = _fixture.BaseAddress };

        // Act
        var refused = await wrong.GetAsync("learning/xapi/statements", TestContext.Current.CancellationToken);
        var about = await anonymous.GetAsync("learning/xapi/about", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, about.StatusCode);
        Assert.Contains("1.0.3", await about.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Preflight_FromAnOpaqueOrigin_IsAnswered_WithoutCredentials()
    {
        // Arrange
        using var anonymous = new HttpClient { BaseAddress = _fixture.BaseAddress };
        using var request = new HttpRequestMessage(HttpMethod.Options, "learning/xapi/statements");

        request.Headers.Add("Origin", "null");
        request.Headers.Add("Access-Control-Request-Method", "PUT");
        request.Headers.Add("Access-Control-Request-Headers", "authorization, content-type, x-experience-api-version");

        // Act
        var response = await anonymous.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("*", Header(response, "Access-Control-Allow-Origin"));
        Assert.Contains("Authorization", Header(response, "Access-Control-Allow-Headers"));
        Assert.Contains(XapiConstants.Headers.Version, Header(response, "Access-Control-Allow-Headers"));
        Assert.Contains("PUT", Header(response, "Access-Control-Allow-Methods"));
        Assert.Null(Header(response, "Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task AlternateRequestSyntax_IsUnwrapped()
    {
        // Arrange
        await EnsureClientAsync();
        using var anonymous = new HttpClient { BaseAddress = _fixture.BaseAddress };
        var id = Guid.NewGuid().ToString();
        var authorization = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(ClientId + ":" + Secret));
        var put = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Authorization"] = authorization,
            [XapiConstants.Headers.Version] = "1.0.3",
            ["Content-Type"] = "application/json",
            ["statementId"] = id,
            ["content"] = Statement().ToJsonString(),
        });
        var get = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Authorization"] = authorization,
            [XapiConstants.Headers.Version] = "1.0.3",
            ["statementId"] = id,
        });

        // Act
        var stored = await anonymous.PostAsync("learning/xapi/statements?method=PUT", put, TestContext.Current.CancellationToken);
        var read = await anonymous.PostAsync("learning/xapi/statements?method=GET", get, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, stored.StatusCode);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(id, JsonNode.Parse(await read.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!["id"]!.GetValue<string>());
    }

    private async Task EnsureClientAsync()
    {
        await _clientLock.WaitAsync(TestContext.Current.CancellationToken);

        try
        {
            if (_clientCreated)
            {
                return;
            }

            await _fixture.InTenantAsync(services => LearningSiteFixture.RunRecipeAsync(services, new JsonObject
            {
                ["name"] = "LearningRecordStoreClientRole",
                ["steps"] = new JsonArray(new JsonObject
                {
                    ["name"] = "Roles",
                    ["Roles"] = new JsonArray(LearningSiteFixture.Role(
                        "LrsClient",
                        LearningXapiPermissions.WriteLearningStatements.Name,
                        LearningXapiPermissions.ReadAllLearningStatements.Name,
                        LearningXapiPermissions.ManageLearningDocuments.Name)),
                }),
            }));

            await _fixture.InTenantAsync(async services =>
            {
                var applications = services.GetRequiredService<IOpenIdApplicationManager>();

                if (await applications.FindByClientIdAsync(ClientId, TestContext.Current.CancellationToken) is null)
                {
                    var descriptor = new OpenIdApplicationDescriptor
                    {
                        ClientId = ClientId,
                        ClientSecret = Secret,
                        ClientType = OpenIddictConstants.ClientTypes.Confidential,
                        DisplayName = "LRS integration client",
                    };

                    descriptor.Roles.Add("LrsClient");
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);

                    await applications.CreateAsync(descriptor, TestContext.Current.CancellationToken);
                }
            });

            _clientCreated = true;
        }
        finally
        {
            _clientLock.Release();
        }
    }

    private HttpClient Client(string clientId, string secret)
    {
        var http = new HttpClient { BaseAddress = _fixture.BaseAddress };

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + secret)));
        http.DefaultRequestHeaders.Add(XapiConstants.Headers.Version, "1.0.3");

        return http;
    }

    private static JsonObject Statement()
        => new()
        {
            ["actor"] = new JsonObject { ["mbox"] = "mailto:integration@example.com", ["name"] = "Integration" },
            ["verb"] = new JsonObject { ["id"] = "http://adlnet.gov/expapi/verbs/experienced" },
            ["object"] = new JsonObject { ["id"] = "http://example.com/integration/activity" },
        };

    private static StringContent Json(JsonNode value)
        => new(value.ToJsonString(), Encoding.UTF8, "application/json");

    private static string Header(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values)
            ? string.Join(", ", values)
            : null;
}
