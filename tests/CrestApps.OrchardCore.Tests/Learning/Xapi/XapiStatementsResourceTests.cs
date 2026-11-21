using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi;
using CrestApps.OrchardCore.Learning.Xapi.Core.Http;
using CrestApps.OrchardCore.Learning.Xapi.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Xapi;

public sealed class XapiStatementsResourceTests
{
    [Fact]
    public async Task Put_StoresTheStatement_WithWhatTheLrsSets()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var id = XapiSamples.NewId();

        var put = await lrs.PutAsync(id, XapiSamples.Simple());
        var get = await lrs.GetAsync(new Dictionary<string, string> { ["statementId"] = id });
        var stored = JsonNode.Parse(get.Text).AsObject();

        Assert.Equal(204, put.Status);
        Assert.Equal(200, get.Status);
        Assert.Equal(id, stored["id"].GetValue<string>());
        Assert.Equal("2026-11-02T12:00:00.000Z", stored["stored"].GetValue<string>());
        Assert.Equal("2026-11-02T12:00:00.000Z", stored["timestamp"].GetValue<string>());
        Assert.Equal("1.0.0", stored["version"].GetValue<string>());
        Assert.Equal("lrs-admin", stored["authority"]["account"]["name"].GetValue<string>());
        Assert.True(get.Headers.ContainsKey(XapiConstants.Headers.ConsistentThrough));
        Assert.Equal("Mon, 02 Nov 2026 12:00:00 GMT", get.Headers["Last-Modified"]);
    }

    [Fact]
    public async Task Put_OfAnExistingId_IsANoOpForTheSameStatement_AndAConflictForAnother()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var id = XapiSamples.NewId();

        await lrs.PutAsync(id, XapiSamples.Simple());
        lrs.Clock.UtcNow = lrs.Clock.UtcNow.AddMinutes(1);

        var same = await lrs.PutAsync(id, XapiSamples.Simple());
        var other = await lrs.PutAsync(id, XapiSamples.Simple(verb: XapiSamples.Attempted));
        var posted = await lrs.PostAsync(XapiSamples.Simple(id, verb: XapiSamples.Attempted));
        var get = await lrs.GetAsync(new Dictionary<string, string> { ["statementId"] = id });

        Assert.Equal(204, same.Status);
        Assert.Equal(409, other.Status);
        Assert.Equal(409, posted.Status);
        Assert.Equal(XapiSamples.Completed, JsonNode.Parse(get.Text)["verb"]["id"].GetValue<string>());
        Assert.Equal("2026-11-02T12:00:00.000Z", JsonNode.Parse(get.Text)["stored"].GetValue<string>());
    }

    [Fact]
    public async Task Put_NeedsTheStatementId_ThatTheBodyDoesNotContradict()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();

        var missing = await lrs.StatementsAsync(XapiRequest.Create("PUT", "statements", XapiSamples.Simple()));
        var contradicted = await lrs.PutAsync(XapiSamples.NewId(), XapiSamples.Simple(XapiSamples.NewId()));
        var array = await lrs.PutAsync(XapiSamples.NewId(), new JsonArray(XapiSamples.Simple()));
        var notJson = await lrs.StatementsAsync(XapiRequest.Create("PUT", "statements", "{ not json", new Dictionary<string, string> { ["statementId"] = XapiSamples.NewId() }));
        var textPlain = XapiRequest.Create("PUT", "statements", XapiSamples.Simple(), new Dictionary<string, string> { ["statementId"] = XapiSamples.NewId() });

        textPlain.Headers["Content-Type"] = "text/plain";

        Assert.Equal(400, missing.Status);
        Assert.Equal(400, contradicted.Status);
        Assert.Equal(400, array.Status);
        Assert.Equal(400, notJson.Status);
        Assert.Equal(400, (await lrs.StatementsAsync(textPlain)).Status);
    }

    [Fact]
    public async Task Post_ReturnsTheIdsInOrder_AndRefusesTheWholeBatchForOneInvalidStatement()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var given = XapiSamples.NewId();

        var single = await lrs.PostAsync(XapiSamples.Simple());
        var batch = await lrs.PostAsync(new JsonArray(XapiSamples.Simple(given), XapiSamples.Full()));
        var invalid = await lrs.PostAsync(new JsonArray(XapiSamples.Simple(XapiSamples.NewId()), new JsonObject { ["actor"] = XapiSamples.Agent() }));
        var duplicate = await lrs.PostAsync(new JsonArray(XapiSamples.Simple(given), XapiSamples.Simple(given)));
        var all = JsonNode.Parse((await lrs.GetAsync(new Dictionary<string, string>())).Text)["statements"].AsArray();

        Assert.Equal(200, single.Status);
        Assert.Single(JsonNode.Parse(single.Text).AsArray());
        Assert.Equal(200, batch.Status);
        Assert.Equal(given, JsonNode.Parse(batch.Text)[0].GetValue<string>());
        Assert.True(Guid.TryParse(JsonNode.Parse(batch.Text)[1].GetValue<string>(), out _));
        Assert.Equal(400, invalid.Status);
        Assert.Equal(400, duplicate.Status);
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task Voiding_HidesTheStatementFromQueries_ButNotFromVoidedStatementId()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var target = XapiSamples.NewId();
        var voiding = XapiSamples.NewId();
        var kept = XapiSamples.NewId();

        await lrs.PutAsync(target, XapiSamples.Simple());
        await lrs.PutAsync(kept, XapiSamples.Simple());
        var voided = await lrs.PutAsync(voiding, XapiSamples.Voiding(target));

        var byId = await lrs.GetAsync(new Dictionary<string, string> { ["statementId"] = target });
        var byVoidedId = await lrs.GetAsync(new Dictionary<string, string> { ["voidedStatementId"] = target });
        var keptByVoidedId = await lrs.GetAsync(new Dictionary<string, string> { ["voidedStatementId"] = kept });
        var query = JsonNode.Parse((await lrs.GetAsync(new Dictionary<string, string>())).Text)["statements"].AsArray().Select(statement => statement["id"].GetValue<string>()).ToList();
        var voidTheVoiding = await lrs.PostAsync(XapiSamples.Voiding(voiding));

        Assert.Equal(204, voided.Status);
        Assert.Equal(404, byId.Status);
        Assert.Equal(200, byVoidedId.Status);
        Assert.Equal(404, keptByVoidedId.Status);
        Assert.DoesNotContain(target, query);
        Assert.Contains(voiding, query);
        Assert.Contains(kept, query);
        Assert.Equal(400, voidTheVoiding.Status);
    }

    [Fact]
    public async Task Query_FiltersByAgentVerbActivityRegistrationAndTime()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var full = XapiSamples.NewId();
        var other = XapiSamples.NewId();

        await lrs.PutAsync(full, XapiSamples.Full());
        lrs.Clock.UtcNow = lrs.Clock.UtcNow.AddMinutes(10);
        await lrs.PutAsync(other, XapiSamples.Simple(mbox: "mailto:other@example.com", verb: XapiSamples.Attempted, activity: XapiSamples.Course));

        Assert.Equal([full], await IdsAsync(lrs, new() { ["agent"] = """{ "mbox": "mailto:learner@example.com" }""" }));
        Assert.Empty(await IdsAsync(lrs, new() { ["agent"] = """{ "mbox": "mailto:instructor@example.com" }""" }));
        Assert.Equal([full], await IdsAsync(lrs, new() { ["agent"] = """{ "mbox": "mailto:instructor@example.com" }""", ["related_agents"] = "true" }));
        Assert.Equal([other, full], await IdsAsync(lrs, new() { ["agent"] = """{ "account": { "homePage": "https://lrs.example.com", "name": "lrs-admin" } }""", ["related_agents"] = "true" }));
        Assert.Equal([other], await IdsAsync(lrs, new() { ["verb"] = XapiSamples.Attempted }));
        Assert.Equal([other], await IdsAsync(lrs, new() { ["activity"] = XapiSamples.Course }));
        Assert.Empty(await IdsAsync(lrs, new() { ["activity"] = "http://www.example.com/meetings/series/267" }));
        Assert.Equal([full], await IdsAsync(lrs, new() { ["activity"] = "http://www.example.com/meetings/series/267", ["related_activities"] = "true" }));
        Assert.Equal([full], await IdsAsync(lrs, new() { ["registration"] = XapiSamples.Registration }));
        Assert.Equal([other], await IdsAsync(lrs, new() { ["since"] = "2026-11-02T12:05:00Z" }));
        Assert.Equal([full], await IdsAsync(lrs, new() { ["until"] = "2026-11-02T12:05:00Z" }));
        Assert.Equal([full, other], await IdsAsync(lrs, new() { ["ascending"] = "true" }));
    }

    [Fact]
    public async Task Query_FollowsTheMoreLink_UntilEveryStatementIsRead()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var ids = new List<string>();

        for (var index = 0; index < 5; index++)
        {
            var id = XapiSamples.NewId();

            ids.Add(id);
            lrs.Clock.UtcNow = lrs.Clock.UtcNow.AddSeconds(1);
            await lrs.PutAsync(id, XapiSamples.Simple());
        }

        var read = new List<string>();
        var page = JsonNode.Parse((await lrs.GetAsync(new Dictionary<string, string> { ["limit"] = "2", ["ascending"] = "true" })).Text);
        var pages = 1;

        read.AddRange(page["statements"].AsArray().Select(statement => statement["id"].GetValue<string>()));

        while (page["more"].GetValue<string>() is { Length: > 0 } more)
        {
            Assert.StartsWith("/learning/xapi/statements?more=", more);

            // A statement stored after the first page is not in the rest of this result.
            await lrs.PutAsync(XapiSamples.NewId(), XapiSamples.Simple());

            var token = Uri.UnescapeDataString(more[(more.IndexOf("more=", StringComparison.Ordinal) + 5)..]);
            page = JsonNode.Parse((await lrs.GetAsync(new Dictionary<string, string> { ["more"] = token })).Text);
            read.AddRange(page["statements"].AsArray().Select(statement => statement["id"].GetValue<string>()));
            pages++;
        }

        Assert.Equal(ids, read);
        Assert.Equal(3, pages);
    }

    [Fact]
    public async Task Query_RefusesUnknownOrCombinedParameters()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var id = XapiSamples.NewId();

        Assert.Equal(400, (await lrs.GetAsync(new Dictionary<string, string> { ["statementId"] = id, ["verb"] = XapiSamples.Completed })).Status);
        Assert.Equal(400, (await lrs.GetAsync(new Dictionary<string, string> { ["statementId"] = id, ["voidedStatementId"] = id })).Status);
        Assert.Equal(400, (await lrs.GetAsync(new Dictionary<string, string> { ["unknown"] = "1" })).Status);
        Assert.Equal(400, (await lrs.GetAsync(new Dictionary<string, string> { ["agent"] = "{ not json" })).Status);
        Assert.Equal(400, (await lrs.GetAsync(new Dictionary<string, string> { ["agent"] = """{ "name": "no identifier" }""" })).Status);
        Assert.Equal(400, (await lrs.GetAsync(new Dictionary<string, string> { ["limit"] = "-1" })).Status);
        Assert.Equal(400, (await lrs.GetAsync(new Dictionary<string, string> { ["format"] = "short" })).Status);
        Assert.Equal(400, (await lrs.GetAsync(new Dictionary<string, string> { ["since"] = "yesterday" })).Status);
        Assert.Equal(404, (await lrs.GetAsync(new Dictionary<string, string> { ["statementId"] = id, ["format"] = "ids", ["attachments"] = "false" })).Status);
    }

    [Fact]
    public async Task StatementRefs_MatchTheFiltersOfTheStatementTheyTarget_EvenWhenItArrivesLater()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var target = XapiSamples.NewId();
        var referring = XapiSamples.NewId();
        var reference = new JsonObject
        {
            ["actor"] = XapiSamples.Agent("mailto:commenter@example.com"),
            ["verb"] = new JsonObject { ["id"] = "http://adlnet.gov/expapi/verbs/commented" },
            ["object"] = new JsonObject { ["objectType"] = "StatementRef", ["id"] = target },
        };

        await lrs.PutAsync(referring, reference);

        Assert.Empty(await IdsAsync(lrs, new() { ["activity"] = XapiSamples.Course }));

        await lrs.PutAsync(target, XapiSamples.Simple(activity: XapiSamples.Course));

        Assert.Equal([target, referring], await IdsAsync(lrs, new() { ["activity"] = XapiSamples.Course }));
        Assert.Equal([target, referring], await IdsAsync(lrs, new() { ["agent"] = """{ "mbox": "mailto:learner@example.com" }""" }));
    }

    [Fact]
    public async Task Formats_ReduceOrPickLanguages()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var id = XapiSamples.NewId();

        await lrs.PutAsync(id, XapiSamples.Full());

        var ids = JsonNode.Parse((await lrs.GetAsync(new Dictionary<string, string> { ["statementId"] = id, ["format"] = "ids" })).Text);
        var canonical = JsonNode.Parse((await lrs.GetAsync(new Dictionary<string, string> { ["statementId"] = id, ["format"] = "canonical" }, acceptLanguage: "fr-FR, en;q=0.5")).Text);

        Assert.Null(ids["object"]["definition"]);
        Assert.Null(ids["actor"]["name"]);
        Assert.Equal(["fr-FR"], canonical["verb"]["display"].AsObject().Select(pair => pair.Key));
    }

    [Fact]
    public async Task Attachments_AreStoredFromMultipart_AndReturnedWhenAsked()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var id = XapiSamples.NewId();
        var content = Encoding.UTF8.GetBytes("here is a simple attachment");

        var stored = await lrs.StatementsAsync(Multipart("PUT", id, WithAttachment(XapiSamples.Simple(), content), content));
        var plain = await lrs.GetAsync(new Dictionary<string, string> { ["statementId"] = id });
        var withContent = await lrs.GetAsync(new Dictionary<string, string> { ["statementId"] = id, ["attachments"] = "true" });
        var parts = XapiMultipart.Parse(withContent.Body, withContent.ContentType);

        Assert.Equal(204, stored.Status);
        Assert.StartsWith("application/json", plain.ContentType);
        Assert.StartsWith("multipart/mixed", withContent.ContentType);
        Assert.Equal(2, parts.Count);
        Assert.Equal(id, JsonNode.Parse(parts[0].Content)["id"].GetValue<string>());
        Assert.Equal(content, parts[1].Content);
        Assert.Equal(Sha256(content), parts[1].Header(XapiConstants.Headers.Hash));
        Assert.Single(lrs.Files.Stored);
    }

    [Fact]
    public async Task Attachments_WithoutContent_WithAWrongHash_OrWithAnExtraPart_AreRefused()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var content = Encoding.UTF8.GetBytes("here is a simple attachment");

        var noContent = await lrs.PutAsync(XapiSamples.NewId(), WithAttachment(XapiSamples.Simple(), content));
        var wrongHash = Multipart("PUT", XapiSamples.NewId(), WithAttachment(XapiSamples.Simple(), content), Encoding.UTF8.GetBytes("other content"), declaredHash: Sha256(content));
        var extraPart = Multipart("POST", null, XapiSamples.Simple(), content);

        Assert.Equal(400, noContent.Status);
        Assert.Equal(400, (await lrs.StatementsAsync(wrongHash)).Status);
        Assert.Equal(400, (await lrs.StatementsAsync(extraPart)).Status);
        Assert.Empty(lrs.Files.Stored);
    }

    [Fact]
    public async Task SignedStatement_IsStoredWhenItsSignatureMatches_AndRefusedOtherwise()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        using var rsa = RSA.Create(2048);
        var statement = XapiSamples.Simple(XapiSamples.NewId());
        var other = XapiSamples.Simple(XapiSamples.NewId(), verb: XapiSamples.Attempted);
        var good = XapiMultipartAndSignatureTests.Sign(statement, rsa, "RS256", withCertificate: true);
        var bad = XapiMultipartAndSignatureTests.Sign(other, rsa, "RS256", withCertificate: true);

        var signed = await lrs.StatementsAsync(Multipart("POST", null, WithAttachment(statement.DeepClone().AsObject(), good, XapiConstants.AttachmentUsages.Signature, "application/octet-stream"), good));
        var refused = await lrs.StatementsAsync(Multipart("POST", null, WithAttachment(statement.DeepClone().AsObject(), bad, XapiConstants.AttachmentUsages.Signature, "application/octet-stream"), bad));

        Assert.Equal(200, signed.Status);
        Assert.Equal(400, refused.Status);
    }

    [Fact]
    public async Task ReadAccess_IsLimitedToOwnStatements_OrToTheLaunch()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var writer = XapiTestHarness.Client("writer", write: true, readOwn: true);
        var mine = XapiSamples.NewId();
        var theirs = XapiSamples.NewId();
        var launched = XapiSamples.Simple(mbox: "mailto:launched@example.com");

        launched["context"] = new JsonObject { ["registration"] = XapiSamples.Registration };

        await lrs.PutAsync(mine, XapiSamples.Simple(), writer);
        await lrs.PutAsync(theirs, launched);

        var launch = new XapiCredential
        {
            Launch = new XapiLaunchScope { Registration = XapiSamples.Registration, ActorKey = "mbox:mailto:launched@example.com", ActivityId = XapiSamples.Activity },
            AuthorityKey = "launch",
            Authority = XapiSamples.Agent("mailto:lms@example.com"),
        };

        Assert.Equal([mine], await IdsAsync(lrs, [], writer));
        Assert.Equal(404, (await lrs.GetAsync(new Dictionary<string, string> { ["statementId"] = theirs }, writer)).Status);
        Assert.Equal([theirs], await IdsAsync(lrs, [], launch));
        Assert.Equal(404, (await lrs.GetAsync(new Dictionary<string, string> { ["statementId"] = mine }, launch)).Status);
        Assert.Equal(403, (await lrs.GetAsync(new Dictionary<string, string>(), XapiTestHarness.Client("writer-only", write: true))).Status);
        Assert.Equal(403, (await lrs.PostAsync(XapiSamples.Simple(), XapiTestHarness.Client("reader", readAll: true))).Status);
    }

    [Fact]
    public async Task LaunchCredential_StoresOnlyStatementsOfItsActorAndRegistration()
    {
        await using var lrs = await XapiTestHarness.CreateAsync();
        var launch = new XapiCredential
        {
            Launch = new XapiLaunchScope { Registration = XapiSamples.Registration, ActorKey = "mbox:mailto:learner@example.com", ActivityId = XapiSamples.Activity },
            AuthorityKey = "launch",
            Authority = XapiSamples.Agent("mailto:lms@example.com"),
        };
        var inRegistration = XapiSamples.Simple();
        var otherActor = XapiSamples.Simple(mbox: "mailto:other@example.com");

        inRegistration["context"] = new JsonObject { ["registration"] = XapiSamples.Registration };
        otherActor["context"] = new JsonObject { ["registration"] = XapiSamples.Registration };

        Assert.Equal(200, (await lrs.PostAsync(inRegistration, launch)).Status);
        Assert.Equal(403, (await lrs.PostAsync(otherActor, launch)).Status);
        Assert.Equal(403, (await lrs.PostAsync(XapiSamples.Simple(), launch)).Status);
    }

    [Fact]
    public async Task Filters_CanRefuseTheRequest_AndSeeWhatTheLrsAlreadyHolds()
    {
        var filter = new RefusingFilter();
        await using var lrs = await XapiTestHarness.CreateAsync(services => services.AddScoped<IXapiStatementFilter>(_ => filter));
        var id = XapiSamples.NewId();

        var accepted = await lrs.PutAsync(id, XapiSamples.Simple());
        var again = await lrs.PutAsync(id, XapiSamples.Simple());

        filter.Refuse = true;

        var refused = await lrs.PostAsync(XapiSamples.Simple());

        Assert.Equal(204, accepted.Status);
        Assert.Equal(204, again.Status);
        Assert.Contains(id, filter.SeenExisting);
        Assert.Equal(403, refused.Status);
        Assert.Single(await IdsAsync(lrs, []));
    }

    [Fact]
    public async Task Limits_KeepTheStoreBounded()
    {
        await using var lrs = await XapiTestHarness.CreateAsync(services => services.Configure<CrestApps.OrchardCore.Learning.Xapi.Core.XapiOptions>(options => options.MaxStatementsPerRequest = 2));

        var tooMany = await lrs.PostAsync(new JsonArray(XapiSamples.Simple(), XapiSamples.Simple(), XapiSamples.Simple()));

        Assert.Equal(413, tooMany.Status);
    }

    [Fact]
    public async Task TrustedWrites_SkipTheFilters_AndUseTheAuthorityGiven()
    {
        var filter = new RefusingFilter { Refuse = true };
        await using var lrs = await XapiTestHarness.CreateAsync(services => services.AddScoped<IXapiStatementFilter>(_ => filter));
        var statement = XapiSamples.Simple(XapiSamples.NewId());

        var result = await lrs.RunAsync(services => services.GetRequiredService<XapiStatementService>().WriteTrustedAsync([statement], XapiSamples.Agent("mailto:lms@example.com"), "lms"));
        var stored = JsonNode.Parse((await lrs.GetAsync(new Dictionary<string, string> { ["statementId"] = statement["id"].GetValue<string>() })).Text);

        Assert.True(result.Succeeded);
        Assert.Equal("mailto:lms@example.com", stored["authority"]["mbox"].GetValue<string>());
    }

    private static async Task<List<string>> IdsAsync(XapiTestHarness lrs, Dictionary<string, string> query, XapiCredential credential = null)
    {
        var response = await lrs.GetAsync(query, credential);

        Assert.Equal(200, response.Status);

        return JsonNode.Parse(response.Text)["statements"].AsArray().Select(statement => statement["id"].GetValue<string>()).ToList();
    }

    private static JsonObject WithAttachment(JsonObject statement, byte[] content, string usage = "http://example.com/attachment-usage/test", string contentType = "text/plain")
    {
        statement["attachments"] = new JsonArray(new JsonObject
        {
            ["usageType"] = usage,
            ["display"] = new JsonObject { ["en-US"] = "A test attachment" },
            ["contentType"] = contentType,
            ["length"] = content.Length,
            ["sha2"] = Sha256(content),
        });

        return statement;
    }

    private static XapiRequest Multipart(string method, string id, JsonObject statement, byte[] content, string declaredHash = null)
    {
        var (body, contentType) = XapiMultipart.Write(
        [
            new XapiPart { Headers = { ["Content-Type"] = "application/json" }, Content = JsonSerializer.SerializeToUtf8Bytes(statement) },
            new XapiPart
            {
                Headers =
                {
                    ["Content-Type"] = "application/octet-stream",
                    ["Content-Transfer-Encoding"] = "binary",
                    [XapiConstants.Headers.Hash] = declaredHash ?? Sha256(content),
                },
                Content = content,
            },
        ]);

        var request = XapiRequest.Create(method, "statements", body, id is null ? null : new Dictionary<string, string> { ["statementId"] = id });

        request.Headers["Content-Type"] = contentType;

        return request;
    }

    private static string Sha256(byte[] content)
        => Convert.ToHexStringLower(SHA256.HashData(content));

    private sealed class RefusingFilter : IXapiStatementFilter
    {
        public bool Refuse { get; set; }

        public List<string> SeenExisting { get; } = [];

        public async Task OnWritingAsync(XapiStatementWriteContext context, Func<Task> next)
        {
            SeenExisting.AddRange(context.ExistingIds);

            if (Refuse)
            {
                context.Reject(403, "Refused by the test filter.");

                return;
            }

            await next();
        }
    }
}
