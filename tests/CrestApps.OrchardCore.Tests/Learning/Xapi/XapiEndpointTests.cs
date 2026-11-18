using System.Collections.Immutable;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi;
using CrestApps.OrchardCore.Learning.Xapi.Core.Http;
using CrestApps.OrchardCore.Learning.Xapi.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenIddict.Abstractions;
using OrchardCore.OpenId.Abstractions.Managers;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Tests.Learning.Xapi;

public sealed class XapiEndpointTests
{
    [Fact]
    public async Task Requests_NeedAOneDotZeroVersionHeader_ExceptAbout()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var missing = XapiRequest.Create("GET", "statements");
        var newer = XapiRequest.Create("GET", "statements");
        var about = XapiRequest.Create("GET", "about");

        missing.Headers.Remove(XapiConstants.Headers.Version);
        newer.Headers[XapiConstants.Headers.Version] = "2.0.0";
        about.Headers.Remove(XapiConstants.Headers.Version);

        var aboutResponse = await lrs.HandleAsync(about, credential: null);

        Assert.Equal(400, (await lrs.HandleAsync(missing, XapiTestHarness.Admin)).Status);
        Assert.Equal(400, (await lrs.HandleAsync(newer, XapiTestHarness.Admin)).Status);
        Assert.Equal(200, aboutResponse.Status);
        Assert.Contains("1.0.3", JsonNode.Parse(aboutResponse.Text)["version"].AsArray().Select(version => version.GetValue<string>()));
    }

    [Fact]
    public async Task Requests_WithoutACredential_Get401_AndUnknownResources_404()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();

        var anonymous = await lrs.HandleAsync(XapiRequest.Create("GET", "statements"), credential: null);
        var unknown = await lrs.HandleAsync(XapiRequest.Create("GET", "nothing"), XapiTestHarness.Admin);

        Assert.Equal(401, anonymous.Status);
        Assert.Contains("Basic", anonymous.Headers["WWW-Authenticate"]);
        Assert.Equal(404, unknown.Status);
    }

    [Fact]
    public async Task Activities_ReturnTheCanonicalDefinition_JoinedFromStatements()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var later = XapiSamples.Simple();

        later["object"]["definition"] = new JsonObject
        {
            ["name"] = new JsonObject { ["de-DE"] = "Besprechung" },
            ["description"] = new JsonObject { ["en-US"] = "A meeting" },
        };

        await lrs.PostAsync(XapiSamples.Simple());
        await lrs.PostAsync(later);

        var known = JsonNode.Parse((await lrs.HandleAsync(XapiRequest.Create("GET", "activities", query: new Dictionary<string, string> { ["activityId"] = XapiSamples.Activity }), XapiTestHarness.Admin)).Text);
        var unknown = JsonNode.Parse((await lrs.HandleAsync(XapiRequest.Create("GET", "activities", query: new Dictionary<string, string> { ["activityId"] = "http://example.com/unknown" }), XapiTestHarness.Admin)).Text);

        Assert.Equal("Example meeting", known["definition"]["name"]["en-US"].GetValue<string>());
        Assert.Equal("Besprechung", known["definition"]["name"]["de-DE"].GetValue<string>());
        Assert.Equal("A meeting", known["definition"]["description"]["en-US"].GetValue<string>());
        Assert.Equal("http://example.com/unknown", unknown["id"].GetValue<string>());
        Assert.Null(unknown["definition"]);
    }

    [Fact]
    public async Task Agents_ReturnAPerson()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();

        var person = await lrs.HandleAsync(XapiRequest.Create("GET", "agents", query: new Dictionary<string, string> { ["agent"] = """{ "name": "Learner", "mbox": "mailto:learner@example.com" }""" }), XapiTestHarness.Admin);
        var group = await lrs.HandleAsync(XapiRequest.Create("GET", "agents", query: new Dictionary<string, string> { ["agent"] = """{ "objectType": "Group", "mbox": "mailto:team@example.com" }""" }), XapiTestHarness.Admin);

        Assert.Equal("Person", JsonNode.Parse(person.Text)["objectType"].GetValue<string>());
        Assert.Equal("mailto:learner@example.com", JsonNode.Parse(person.Text)["mbox"][0].GetValue<string>());
        Assert.Equal(400, group.Status);
    }

    [Fact]
    public void AlternateSyntax_IsUnwrapped_IntoTheRequestItStandsFor()
    {
        var statement = XapiSamples.Simple().ToJsonString();
        var id = XapiSamples.NewId();
        var form = "X-Experience-API-Version=1.0.3&Content-Type=application%2Fjson&Authorization=" + Uri.EscapeDataString("Basic abc") +
            "&statementId=" + id + "&content=" + Uri.EscapeDataString(statement);
        var posted = XapiRequest.Create("POST", "statements", Encoding.UTF8.GetBytes(form), new Dictionary<string, string> { ["method"] = "PUT" });

        posted.Headers.Remove(XapiConstants.Headers.Version);
        posted.Headers["Content-Type"] = "application/x-www-form-urlencoded";

        var (request, error) = XapiHttp.Unwrap(posted, "PUT");
        var (_, extraParameter) = XapiHttp.Unwrap(XapiRequest.Create("POST", "statements", Encoding.UTF8.GetBytes(form), new Dictionary<string, string> { ["method"] = "PUT", ["statementId"] = id }), "PUT");
        var (_, badMethod) = XapiHttp.Unwrap(posted, "PATCH");

        Assert.Null(error);
        Assert.True(request.Alternate);
        Assert.Equal("PUT", request.Method);
        Assert.Equal(id, request.Param("statementId"));
        Assert.Equal("1.0.3", request.Header(XapiConstants.Headers.Version));
        Assert.Equal("Basic abc", request.Header("Authorization"));
        Assert.Equal("application/json", request.MediaType);
        Assert.Equal(statement, Encoding.UTF8.GetString(request.Body));
        Assert.Equal(400, extraParameter.Status);
        Assert.Equal(400, badMethod.Status);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task Round9H1_AFormPostStandingForAGetOrHead_IsRefused(string method)
    {
        var form = "X-Experience-API-Version=1.0.3&Authorization=" + Uri.EscapeDataString("Bearer launch-token") +
            "&agent=" + Uri.EscapeDataString("""{ "mbox": "mailto:learner@example.com" }""") + "&profileId=page";
        var httpContext = new DefaultHttpContext();

        httpContext.Request.Method = "POST";
        httpContext.Request.QueryString = new QueryString("?method=" + method);
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(form));

        var (request, error) = await XapiHttp.ReadAsync(httpContext, "agents/profile", "/learning/xapi", 4096);

        Assert.Null(request);
        Assert.NotNull(error);
        Assert.Equal(400, error.Status);
    }

    [Fact]
    public async Task Http_ReadsTheRequest_AndWritesTheVersionAndCorsHeaders()
    {
        var httpContext = new DefaultHttpContext();

        httpContext.Request.Method = "PUT";
        httpContext.Request.QueryString = new QueryString("?statementId=" + XapiSamples.Registration);
        httpContext.Request.Headers[XapiConstants.Headers.Version] = "1.0.3";
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

        var (request, error) = await XapiHttp.ReadAsync(httpContext, "statements", "/learning/xapi", 1024);
        var (_, tooLarge) = await XapiHttp.ReadAsync(new DefaultHttpContext { Request = { Body = new MemoryStream(new byte[2048]) } }, "statements", "/learning/xapi", 1024);

        Assert.Null(error);
        Assert.Equal("PUT", request.Method);
        Assert.Equal(XapiSamples.Registration, request.Param("statementId"));
        Assert.Equal("{}", Encoding.UTF8.GetString(request.Body));
        Assert.Equal(413, tooLarge.Status);

        var response = new DefaultHttpContext();

        response.Response.Body = new MemoryStream();
        await XapiHttp.WriteAsync(response, XapiResponse.Error(400, "bad"), head: false);

        Assert.Equal(400, response.Response.StatusCode);
        Assert.Equal("1.0.3", response.Response.Headers[XapiConstants.Headers.Version]);
        Assert.Equal("*", response.Response.Headers.AccessControlAllowOrigin);
        Assert.Contains(XapiConstants.Headers.ConsistentThrough, response.Response.Headers.AccessControlExposeHeaders.ToString());
        Assert.False(response.Response.Headers.ContainsKey("Access-Control-Allow-Credentials"));

        var preflight = new DefaultHttpContext();

        XapiHttp.WritePreflight(preflight);

        Assert.Equal(204, preflight.Response.StatusCode);
        Assert.Contains("Authorization", preflight.Response.Headers.AccessControlAllowHeaders.ToString());
        Assert.Contains(XapiConstants.Headers.Version, preflight.Response.Headers.AccessControlAllowHeaders.ToString());
        Assert.Contains("PUT", preflight.Response.Headers.AccessControlAllowMethods.ToString());
        Assert.Equal("*", preflight.Response.Headers.AccessControlAllowOrigin);
        Assert.False(preflight.Response.Headers.ContainsKey("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Basic_WithAnOpenIdClientAndItsSecret_GetsThePermissionsOfItsRoles()
    {
        await using var lrs = await XapiTestHarness.CreateAsync(AddOpenId);

        var credential = await AuthenticateAsync(lrs, "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("lrs-client:secret")));

        Assert.NotNull(credential);
        Assert.Equal("lrs-client", credential.ClientId);
        Assert.True(credential.CanWriteStatements);
        Assert.True(credential.CanReadOwnStatements);
        Assert.False(credential.CanReadAllStatements);
        Assert.False(credential.CanManageDocuments);
        Assert.Equal("lrs-client", credential.Authority["account"]["name"].GetValue<string>());
        Assert.Equal("client:lrs-client", credential.AuthorityKey);
    }

    [Theory]
    [InlineData("lrs-client:wrong")]
    [InlineData("public-client:secret")]
    [InlineData("unknown:secret")]
    [InlineData("no-separator")]
    public async Task Basic_WithAWrongSecret_APublicClient_OrAnUnknownClient_IsRefused(string userAndPassword)
    {
        await using var lrs = await XapiTestHarness.CreateAsync(AddOpenId);

        Assert.Null(await AuthenticateAsync(lrs, "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(userAndPassword))));
    }

    [Fact]
    public async Task TheSignInCookie_AloneIsNeverACredential()
    {
        await using var lrs = await XapiTestHarness.CreateAsync(AddOpenId);

        // A signed-in user who holds every LRS permission, with no Authorization header.
        var credential = await AuthenticateAsync(lrs, authorization: null, user: CalendarsUser());

        Assert.Null(credential);
    }

    [Fact]
    public async Task Bearer_UsesTheApiScheme()
    {
        await using var lrs = await XapiTestHarness.CreateAsync(AddOpenId);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(OpenIddictConstants.Claims.ClientId, "token-client"),
            new Claim(ClaimTypes.Name, "Token client"),
            new Claim(Permission.ClaimType, LearningXapiPermissions.ReadAllLearningStatements.Name),
        ], "Bearer", ClaimTypes.Name, ClaimTypes.Role));

        var credential = await AuthenticateAsync(lrs, "Bearer abc", apiPrincipal: principal);
        var refused = await AuthenticateAsync(lrs, "Bearer abc", apiPrincipal: null);

        Assert.Equal("token-client", credential.ClientId);
        Assert.True(credential.CanReadAllStatements);
        Assert.True(credential.CanReadOwnStatements);
        Assert.False(credential.CanWriteStatements);
        Assert.Null(refused);
    }

    [Fact]
    public async Task LaunchTokens_OpenTheirScope_UntilTheyExpire()
    {
        await using var lrs = await XapiTestHarness.CreateAsync(AddOpenId);
        var scope = new XapiLaunchScope
        {
            Kind = "cmi5",
            SessionId = "session-1",
            Registration = XapiSamples.Registration,
            ActivityId = XapiSamples.Activity,
            ActorKey = "account:https://site.example.com|u1",
            ExpiresUtc = lrs.Clock.UtcNow.AddHours(1),
        };

        var value = await lrs.RunAsync(services => Task.FromResult(services.GetRequiredService<XapiLaunchTokens>().CreateBasicValue(scope)));
        var credential = await AuthenticateAsync(lrs, "Basic " + value);
        var token = Encoding.UTF8.GetString(Convert.FromBase64String(value)).Split(':', 2)[1];
        var tampered = Convert.ToBase64String(Encoding.UTF8.GetBytes(XapiLaunchTokens.BasicUserName + ":" + token[..^2] + "AA"));

        Assert.True(credential.IsLaunch);
        Assert.Equal("session-1", credential.Launch.SessionId);
        Assert.Equal(XapiSamples.Registration, credential.Launch.Registration);
        Assert.False(credential.CanWriteStatements);
        Assert.Null(await AuthenticateAsync(lrs, "Basic " + tampered));

        lrs.Clock.UtcNow = lrs.Clock.UtcNow.AddHours(2);

        Assert.Null(await AuthenticateAsync(lrs, "Basic " + value));
    }

    private static ClaimsPrincipal CalendarsUser()
        => CrestApps.OrchardCore.Tests.Calendars.CalendarTestHarness.User(
            "admin",
            LearningXapiPermissions.WriteLearningStatements.Name,
            LearningXapiPermissions.ReadAllLearningStatements.Name,
            LearningXapiPermissions.ManageLearningDocuments.Name);

    private static void AddOpenId(IServiceCollection services)
    {
        var confidential = new object();
        var publicClient = new object();
        var applications = new Mock<IOpenIdApplicationManager>();

        applications.Setup(manager => manager.FindByClientIdAsync("lrs-client", It.IsAny<CancellationToken>())).ReturnsAsync(confidential);
        applications.Setup(manager => manager.FindByClientIdAsync("public-client", It.IsAny<CancellationToken>())).ReturnsAsync(publicClient);
        applications.Setup(manager => manager.HasClientTypeAsync(confidential, OpenIddictConstants.ClientTypes.Confidential, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        applications.Setup(manager => manager.HasClientTypeAsync(publicClient, OpenIddictConstants.ClientTypes.Confidential, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        applications.Setup(manager => manager.ValidateClientSecretAsync(confidential, "secret", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        applications.Setup(manager => manager.ValidateClientSecretAsync(publicClient, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        applications.Setup(manager => manager.GetDisplayNameAsync(confidential, It.IsAny<CancellationToken>())).ReturnsAsync("LRS client");
        applications.Setup(manager => manager.GetRolesAsync(confidential, It.IsAny<CancellationToken>())).ReturnsAsync(ImmutableArray.Create("LrsWriter"));

        var roles = new Mock<IRoleService>();

        roles.Setup(service => service.GetRoleClaimsAsync("LrsWriter", It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new Claim(Permission.ClaimType, LearningXapiPermissions.WriteLearningStatements.Name),
            new Claim(Permission.ClaimType, LearningXapiPermissions.ReadOwnLearningStatements.Name),
        ]);

        services.AddSingleton(applications.Object);
        services.AddSingleton(roles.Object);
    }

    private static Task<XapiCredential> AuthenticateAsync(XapiTestHarness lrs, string authorization, ClaimsPrincipal user = null, ClaimsPrincipal apiPrincipal = null)
        => lrs.RunAsync(services =>
        {
            var authentication = new Mock<IAuthenticationService>();

            authentication.Setup(service => service.AuthenticateAsync(It.IsAny<HttpContext>(), "Api"))
                .ReturnsAsync(apiPrincipal is null ? AuthenticateResult.NoResult() : AuthenticateResult.Success(new AuthenticationTicket(apiPrincipal, "Api")));

            var requestServices = new ServiceCollection()
                .AddSingleton(authentication.Object)
                .BuildServiceProvider();
            var httpContext = new DefaultHttpContext { RequestServices = requestServices };

            if (user is not null)
            {
                httpContext.User = user;
            }

            var request = XapiRequest.Create("GET", "statements");

            if (authorization is not null)
            {
                request.Headers["Authorization"] = authorization;
            }

            return services.GetRequiredService<XapiAuthenticator>().AuthenticateAsync(httpContext, request);
        });
}
