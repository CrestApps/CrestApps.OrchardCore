using System.Net;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Reconciliation repairs calls the platform has a record of. This covers the ones it does not: a call placed
/// just before the process died, so no interaction was ever written. The person is connected to a platform that
/// has no idea they exist, no webhook will ever produce a record for them, and nothing else in the system will
/// ever notice.
/// </summary>
public sealed class TelnyxOrphanedCallReconcilerTests
{
    [Fact]
    public async Task ACallThePlatformKnowsAbout_IsLeftAlone()
    {
        // Arrange
        var handler = ListingOneCall("call-1", durationSeconds: 600);
        var interactions = KnownCalls("call-1");
        var reconciler = CreateReconciler(handler, interactions, TelnyxOrphanedCallHandling.EndCall);

        // Act
        var result = await reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, result.OrphansFound);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ACallNothingHasARecordOf_IsFound()
    {
        // Arrange
        var handler = ListingOneCall("call-1", durationSeconds: 600);
        var reconciler = CreateReconciler(handler, KnownCalls(), TelnyxOrphanedCallHandling.Report);

        // Act
        var result = await reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, result.OrphansFound);
    }

    [Fact]
    public async Task ByDefault_AnOrphanIsReportedAndLeftConnected()
    {
        // Arrange
        // Hanging up on a live person is the more destructive of the two options, so it is never what happens
        // unless a deployment asks for it: a call the platform lost may still be a conversation between two
        // people who can hear each other perfectly well.
        var handler = ListingOneCall("call-1", durationSeconds: 600);
        var reconciler = CreateReconciler(handler, KnownCalls(), TelnyxOrphanedCallHandling.Report);

        // Act
        var result = await reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, result.OrphansFound);
        Assert.Equal(0, result.OrphansEnded);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task WhenTheDeploymentAsksForIt_AnOrphanIsToldWhatHappenedAndReleased()
    {
        // Arrange
        // Silence is the worst outcome for the person on the call. If a deployment would rather end an orphan
        // than leave it hanging, it is ended with an explanation first.
        var handler = ListingOneCall("call-1", durationSeconds: 600);
        var reconciler = CreateReconciler(handler, KnownCalls(), TelnyxOrphanedCallHandling.EndCall);

        // Act
        var result = await reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, result.OrphansEnded);
        Assert.Equal("/v2/calls/call-1/actions/speak", handler.Requests[1].Path);
        Assert.Equal("/v2/calls/call-1/actions/hangup", handler.Requests[2].Path);
    }

    [Fact]
    public async Task AVeryNewCall_IsLeftAlone()
    {
        // Arrange
        // A call that has been up for seconds is far more likely to be one whose interaction is still being
        // written than one that was lost. Treating it as an orphan would hang up on calls that are working.
        var handler = ListingOneCall("call-1", durationSeconds: 3);
        var reconciler = CreateReconciler(handler, KnownCalls(), TelnyxOrphanedCallHandling.EndCall);

        // Act
        var result = await reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, result.OrphansFound);
    }

    [Fact]
    public async Task EveryPage_IsWalked()
    {
        // Arrange
        // The orphan is as likely to be on the last page as the first, and a busy connection has several.
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, """
                {
                  "data": [ { "call_control_id": "call-1", "call_duration": 600 } ],
                  "meta": { "next_page_token": "page-2" }
                }
                """)
            .RespondWith(HttpStatusCode.OK, """
                { "data": [ { "call_control_id": "call-2", "call_duration": 600 } ] }
                """);
        var reconciler = CreateReconciler(handler, KnownCalls(), TelnyxOrphanedCallHandling.Report);

        // Act
        var result = await reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.OrphansFound);
    }

    [Fact]
    public async Task WhenTheProviderRefusesTheListing_NothingIsAssumedToBeAnOrphan()
    {
        // Arrange
        // An expired API key must not be read as "every call on this connection is lost" and act on it.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.Unauthorized);
        var reconciler = CreateReconciler(handler, KnownCalls(), TelnyxOrphanedCallHandling.EndCall);

        // Act
        var result = await reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(0, result.OrphansFound);
        Assert.Equal(0, result.OrphansEnded);
    }

    [Fact]
    public async Task WithNoConnectionConfigured_NothingIsAsked()
    {
        // Arrange
        // A tenant that has not finished configuring Telnyx has no connection to list, and asking anyway just
        // logs a provider error every time the task runs.
        var handler = new RecordingHttpMessageHandler();
        var reconciler = CreateReconciler(handler, KnownCalls(), TelnyxOrphanedCallHandling.Report, connectionId: null);

        // Act
        var result = await reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(handler.Requests);
    }

    private static RecordingHttpMessageHandler ListingOneCall(string callControlId, int durationSeconds)
        => new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, $$"""
            { "data": [ { "call_control_id": "{{callControlId}}", "call_duration": {{durationSeconds}} } ] }
            """);

    private static ITelephonyInteractionStore KnownCalls(params string[] callIds)
    {
        var store = new Mock<ITelephonyInteractionStore>();

        store.Setup(x => x.FindByProviderCallIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string callId, CancellationToken _) =>
                callIds.Contains(callId) ? new TelephonyInteraction { CallId = callId } : null);

        return store.Object;
    }

    private static TelnyxOrphanedCallReconciler CreateReconciler(
        HttpMessageHandler handler,
        ITelephonyInteractionStore interactions,
        TelnyxOrphanedCallHandling handling,
        string connectionId = "connection-1")
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.telnyx.com/v2/"),
        };

        var options = new TelnyxOptions
        {
            ApiBaseUrl = "https://api.telnyx.com/v2/",
            ApiKey = "test-api-key",
            ConnectionId = connectionId,
            OrphanedCallHandling = handling,
        };

        var apiClient = new TelnyxApiClient(
            httpClient,
            new OptionsWrapper<TelnyxOptions>(options),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);

        return new TelnyxOrphanedCallReconciler(
            apiClient,
            interactions,
            new TestOptionsMonitor<TelnyxOptions>(options),
            NullLogger<TelnyxOrphanedCallReconciler>.Instance);
    }
}
