using System.Net;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Starting and resuming the recording of a Contact Center call on Telnyx.
/// </summary>
/// <remarks>
/// Telnyx answers <c>record_resume</c> with 200 on a call that has no recording at all. A start used to try a resume
/// first and fall back to <c>record_start</c> only when the resume failed, so it took that 200 as success and no queue
/// or dialer call was ever recorded, while the interaction said it was.
/// </remarks>
public sealed class TelnyxContactCenterRecordingTests
{
    [Theory]
    [InlineData(RecordingState.None)]
    [InlineData(RecordingState.Stopped)]
    public async Task RecordingACallThatIsNotPaused_SendsRecordStart_AndNeverProbesWithAResume(RecordingState previousState)
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = TelnyxContactCenterProviderFactory.Create(handler);

        // Act
        var result = await provider.SetRecordingStateAsync(
            CreateRequest(RecordingState.Recording, previousState),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        var request = Assert.Single(handler.Requests);
        Assert.EndsWith("/calls/ctrl-1/actions/record_start", request.Path, StringComparison.Ordinal);
        Assert.Contains("\"channels\"", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecordingAPausedCall_ResumesTheSameRecording()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = TelnyxContactCenterProviderFactory.Create(handler);

        // Act
        var result = await provider.SetRecordingStateAsync(
            CreateRequest(RecordingState.Recording, RecordingState.Paused),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        var request = Assert.Single(handler.Requests);
        Assert.EndsWith("/calls/ctrl-1/actions/record_resume", request.Path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecordingAPausedCallWhoseRecordingTelnyxNoLongerHolds_StartsANewRecording()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90055","title":"No recording in progress"}]}""")
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = TelnyxContactCenterProviderFactory.Create(handler);

        // Act
        var result = await provider.SetRecordingStateAsync(
            CreateRequest(RecordingState.Recording, RecordingState.Paused),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Collection(
            handler.Requests,
            request => Assert.EndsWith("/actions/record_resume", request.Path, StringComparison.Ordinal),
            request => Assert.EndsWith("/actions/record_start", request.Path, StringComparison.Ordinal));
    }

    [Fact]
    public async Task PausingARecording_SendsRecordPause()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = TelnyxContactCenterProviderFactory.Create(handler);

        // Act
        var result = await provider.SetRecordingStateAsync(
            CreateRequest(RecordingState.Paused, RecordingState.Recording),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        var request = Assert.Single(handler.Requests);
        Assert.EndsWith("/calls/ctrl-1/actions/record_pause", request.Path, StringComparison.Ordinal);
    }

    private static ContactCenterVoiceRecordingRequest CreateRequest(RecordingState state, RecordingState previousState)
        => new()
        {
            InteractionId = "interaction-1",
            ProviderCallId = "ctrl-1",
            State = state,
            PreviousState = previousState,
        };
}
