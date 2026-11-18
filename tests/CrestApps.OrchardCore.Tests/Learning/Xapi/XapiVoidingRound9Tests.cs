using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Xapi;

namespace CrestApps.OrchardCore.Tests.Learning.Xapi;

/// <summary>
/// Review round 9, M1: a voiding statement is checked against its target. A launch may void only a statement it could
/// read and that a launch wrote (never one of the LMS's own, such as satisfied), and a client only a statement in its read
/// scope.
/// </summary>
public sealed class XapiVoidingRound9Tests
{
    [Fact]
    public async Task Round9M1_ALaunchVoidsOnlyWhatItWrote_AndAClientOnlyWhatItMayRead()
    {
        // Arrange: in the learner's registration the LMS stored a statement with its own authority and the launch stored
        // one; another learner has a statement too.
        await using var lrs = await XapiTestHarness.CreateAsync();
        var lms = XapiTestHarness.Client("lms", write: true, readAll: true);
        var writer = XapiTestHarness.Client("writer", write: true);
        var launch = new XapiCredential
        {
            Launch = new XapiLaunchScope { Registration = XapiSamples.Registration, ActorKey = "mbox:mailto:learner@example.com", ActivityId = XapiSamples.Activity },
            AuthorityKey = "launch:cmi5",
            Authority = XapiSamples.Agent("mailto:lms@example.com"),
        };

        var lmsId = XapiSamples.NewId();
        var launchedId = XapiSamples.NewId();
        var otherId = XapiSamples.NewId();
        var writerId = XapiSamples.NewId();

        Assert.Equal(204, (await lrs.PutAsync(lmsId, InRegistration(XapiSamples.Simple()), lms)).Status);
        Assert.Equal(204, (await lrs.PutAsync(launchedId, InRegistration(XapiSamples.Simple()), launch)).Status);
        Assert.Equal(204, (await lrs.PutAsync(otherId, XapiSamples.Simple(mbox: "mailto:other@example.com"), lms)).Status);
        Assert.Equal(204, (await lrs.PutAsync(writerId, XapiSamples.Simple(), writer)).Status);

        // Act
        var launchVoidsTheLms = await lrs.PostAsync(LaunchVoiding(lmsId), launch);
        var launchVoidsAnother = await lrs.PostAsync(LaunchVoiding(otherId), launch);
        var writerVoidsTheLms = await lrs.PostAsync(XapiSamples.Voiding(lmsId), writer);
        var launchVoidsItsOwn = await lrs.PostAsync(LaunchVoiding(launchedId), launch);
        var writerVoidsItsOwn = await lrs.PostAsync(XapiSamples.Voiding(writerId), writer);

        // Assert
        Assert.Equal(403, launchVoidsTheLms.Status);
        Assert.Equal(403, launchVoidsAnother.Status);
        Assert.Equal(403, writerVoidsTheLms.Status);
        Assert.Equal(200, launchVoidsItsOwn.Status);
        Assert.Equal(200, writerVoidsItsOwn.Status);
        Assert.Equal(200, (await lrs.GetAsync(new Dictionary<string, string> { ["statementId"] = lmsId })).Status);
        Assert.Equal(200, (await lrs.GetAsync(new Dictionary<string, string> { ["statementId"] = otherId })).Status);
        Assert.Equal(200, (await lrs.GetAsync(new Dictionary<string, string> { ["voidedStatementId"] = launchedId })).Status);
    }

    [Fact]
    public async Task Round10_2_AnEarlyVoid_AppliesToALaterTarget_OnlyWhenItsSenderCouldHaveVoidedIt()
    {
        // Arrange: a launch, a client that writes only, and the LMS that reads every statement.
        await using var lrs = await XapiTestHarness.CreateAsync();
        var lms = XapiTestHarness.Client("lms", write: true, readAll: true);
        var writer = XapiTestHarness.Client("writer", write: true);
        var launch = new XapiCredential
        {
            Launch = new XapiLaunchScope { Registration = XapiSamples.Registration, ActorKey = "mbox:mailto:learner@example.com", ActivityId = XapiSamples.Activity },
            AuthorityKey = "launch:cmi5",
            Authority = XapiSamples.Agent("mailto:lms@example.com"),
        };

        var lmsTarget = XapiSamples.NewId();
        var writerTarget = XapiSamples.NewId();
        var ownTarget = XapiSamples.NewId();
        var readAllTarget = XapiSamples.NewId();

        // Act: each void arrives before its target, whose id the sender predicted.
        Assert.Equal(200, (await lrs.PostAsync(LaunchVoiding(lmsTarget), launch)).Status);
        Assert.Equal(200, (await lrs.PostAsync(XapiSamples.Voiding(writerTarget), writer)).Status);
        Assert.Equal(200, (await lrs.PostAsync(LaunchVoiding(ownTarget), launch)).Status);
        Assert.Equal(200, (await lrs.PostAsync(XapiSamples.Voiding(readAllTarget), lms)).Status);

        Assert.Equal(204, (await lrs.PutAsync(lmsTarget, InRegistration(XapiSamples.Simple(mbox: "mailto:learner@example.com")), lms)).Status);
        Assert.Equal(204, (await lrs.PutAsync(writerTarget, XapiSamples.Simple(), lms)).Status);
        Assert.Equal(204, (await lrs.PutAsync(ownTarget, InRegistration(XapiSamples.Simple(mbox: "mailto:learner@example.com")), launch)).Status);
        Assert.Equal(204, (await lrs.PutAsync(readAllTarget, XapiSamples.Simple(), writer)).Status);

        // Assert: the LMS's statement and another authority's stay valid; the launch's own and any statement voided by
        // a client that reads all are voided.
        Assert.Equal(200, (await lrs.GetAsync(new Dictionary<string, string> { ["statementId"] = lmsTarget })).Status);
        Assert.Equal(200, (await lrs.GetAsync(new Dictionary<string, string> { ["statementId"] = writerTarget })).Status);
        Assert.Equal(200, (await lrs.GetAsync(new Dictionary<string, string> { ["voidedStatementId"] = ownTarget })).Status);
        Assert.Equal(200, (await lrs.GetAsync(new Dictionary<string, string> { ["voidedStatementId"] = readAllTarget })).Status);
    }

    private static JsonObject InRegistration(JsonObject statement)
    {
        statement["context"] = new JsonObject { ["registration"] = XapiSamples.Registration };

        return statement;
    }

    private static JsonObject LaunchVoiding(string targetId)
    {
        var voiding = XapiSamples.Voiding(targetId);

        voiding["actor"] = XapiSamples.Agent("mailto:learner@example.com", "Learner");

        return InRegistration(voiding);
    }
}
