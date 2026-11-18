using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi;
using CrestApps.OrchardCore.Learning.Xapi.Core;
using CrestApps.OrchardCore.Learning.Xapi.Core.Http;
using CrestApps.OrchardCore.Learning.Xapi.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenIddict.Abstractions;
using OrchardCore.Locking.Distributed;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Learning.Xapi;

/// <summary>
/// Review round 9, Low items of the Learning Record Store: a user's bearer token is not a client credential; a more
/// link pages by the order statements were stored in, so a statement voided between pages skips nothing; document
/// writes take a lock per document; the attachments of a request are limited.
/// </summary>
public sealed class XapiLowRound9Tests
{
    [Fact]
    public async Task Round9Low_ABearerTokenOfAUser_IsNotAClientCredential()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var userToken = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(OpenIddictConstants.Claims.ClientId, "spa-client"),
            new Claim(OpenIddictConstants.Claims.Subject, "user-1"),
            new Claim(Permission.ClaimType, LearningXapiPermissions.ReadOwnLearningStatements.Name),
        ], "Bearer"));
        var clientToken = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(OpenIddictConstants.Claims.ClientId, "service-client"),
            new Claim(OpenIddictConstants.Claims.Subject, "service-client"),
            new Claim(Permission.ClaimType, LearningXapiPermissions.ReadOwnLearningStatements.Name),
        ], "Bearer"));

        Assert.Null(await BearerAsync(lrs, userToken));
        Assert.Equal("service-client", (await BearerAsync(lrs, clientToken))?.ClientId);
    }

    [Fact]
    public async Task Round9Low_AStatementVoidedBetweenPages_SkipsNothingOnTheNextPage()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var ids = Enumerable.Range(0, 5).Select(_ => XapiSamples.NewId()).ToList();

        foreach (var id in ids)
        {
            await lrs.PutAsync(id, XapiSamples.Simple());
            lrs.Clock.UtcNow = lrs.Clock.UtcNow.AddSeconds(1);
        }

        // Newest first: the first page is the last two stored.
        var first = JsonNode.Parse((await lrs.GetAsync(new Dictionary<string, string> { ["limit"] = "2" })).Text);
        var firstIds = first["statements"].AsArray().Select(statement => statement["id"].GetValue<string>()).ToList();
        var more = first["more"].GetValue<string>();

        await lrs.PostAsync(XapiSamples.Voiding(firstIds[0]));

        var token = Uri.UnescapeDataString(more[(more.IndexOf("more=", StringComparison.Ordinal) + 5)..]);
        var second = JsonNode.Parse((await lrs.GetAsync(new Dictionary<string, string> { ["more"] = token })).Text);
        var secondIds = second["statements"].AsArray().Select(statement => statement["id"].GetValue<string>()).ToList();

        Assert.Equal([ids[4], ids[3]], firstIds);
        Assert.Equal([ids[2], ids[1]], secondIds);
    }

    [Fact]
    public async Task Round9Low_ADocumentWrite_TakesALockForItsDocument()
    {
        var locks = new RecordingLock();
        await using var lrs = await XapiTestHarness.CreateAsync(services => services.AddSingleton<IDistributedLock>(locks));
        var state = new Dictionary<string, string>
        {
            ["activityId"] = XapiSamples.Activity,
            ["agent"] = """{ "mbox": "mailto:learner@example.com" }""",
            ["stateId"] = "bookmark",
        };

        Assert.Equal(204, (await lrs.DocumentAsync("POST", "activities/state", state, new JsonObject { ["page"] = 1 })).Status);
        Assert.Contains(locks.Keys, key => key.StartsWith("LearningXapi:Document:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Round9Low_AttachmentsOverTheLimit_AreRefused()
    {
        await using var lrs = await XapiTestHarness.CreateAsync(services => services.Configure<XapiOptions>(options => options.MaxAttachmentBytes = 10));
        var content = Encoding.UTF8.GetBytes("more than ten bytes of attachment");
        var statement = XapiSamples.Simple(XapiSamples.NewId());

        statement["attachments"] = new JsonArray(new JsonObject
        {
            ["usageType"] = "http://example.com/attachment-usage/test",
            ["display"] = new JsonObject { ["en-US"] = "Attachment" },
            ["contentType"] = "text/plain",
            ["length"] = content.Length,
            ["sha2"] = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content)),
        });

        var (body, contentType) = XapiMultipart.Write(
        [
            new XapiPart { Headers = { ["Content-Type"] = "application/json" }, Content = Encoding.UTF8.GetBytes(statement.ToJsonString()) },
            new XapiPart { Headers = { ["Content-Type"] = "text/plain", ["Content-Transfer-Encoding"] = "binary", [XapiConstants.Headers.Hash] = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content)) }, Content = content },
        ]);
        var request = XapiRequest.Create("POST", "statements", body);

        request.Headers["Content-Type"] = contentType;

        Assert.Equal(413, (await lrs.StatementsAsync(request)).Status);
    }

    private static Task<XapiCredential> BearerAsync(XapiTestHarness lrs, ClaimsPrincipal principal)
        => lrs.RunAsync(services =>
        {
            var authentication = new Mock<IAuthenticationService>();

            authentication.Setup(service => service.AuthenticateAsync(It.IsAny<HttpContext>(), "Api"))
                .ReturnsAsync(AuthenticateResult.Success(new AuthenticationTicket(principal, "Api")));

            var httpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton(authentication.Object).BuildServiceProvider() };
            var request = XapiRequest.Create("GET", "statements");

            request.Headers["Authorization"] = "Bearer token";

            return services.GetRequiredService<XapiAuthenticator>().AuthenticateAsync(httpContext, request);
        });
}
