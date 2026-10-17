using System.Net;
using System.Text;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// The Telnyx end of the voice media cache. What matters is that a copy Telnyx has lost is reported lost, so it
/// is made again from the master copy, and that a Telnyx outage is not mistaken for a lost copy.
/// </summary>
public sealed class TelnyxVoiceMediaProvisionerTests
{
    [Fact]
    public async Task GetAvailabilityAsync_WhenTelnyxHoldsTheMedia_ReportsItAvailable()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().RespondWith(HttpStatusCode.OK, """{"data":{"media_name":"cc-voice-media-1"}}""");
        var provisioner = CreateProvisioner(handler);

        // Act
        var availability = await provisioner.GetAvailabilityAsync("cc-voice-media-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(VoiceMediaAvailability.Available, availability);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/v2/media/cc-voice-media-1", request.Path);
        Assert.Equal("Bearer test-api-key", request.Authorization);
    }

    [Fact]
    public async Task GetAvailabilityAsync_WhenTelnyxDoesNotKnowTheMedia_ReportsItMissing()
    {
        // Arrange
        // Deleted, expired or uploaded to another account: all are a 404, and all mean the copy has to be made again.
        var handler = new RecordingHttpMessageHandler().RespondWith(HttpStatusCode.NotFound, """{"errors":[{"code":"10005"}]}""");
        var provisioner = CreateProvisioner(handler);

        // Act
        var availability = await provisioner.GetAvailabilityAsync("cc-voice-media-gone", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(VoiceMediaAvailability.Missing, availability);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task GetAvailabilityAsync_WhenTelnyxCannotAnswer_ReportsUnknown(HttpStatusCode statusCode)
    {
        // Arrange
        // An answer that says nothing about the clip must not make every call upload the clip again.
        var handler = new RecordingHttpMessageHandler().RespondWith(statusCode);
        var provisioner = CreateProvisioner(handler);

        // Act
        var availability = await provisioner.GetAvailabilityAsync("cc-voice-media-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(VoiceMediaAvailability.Unknown, availability);
    }

    [Fact]
    public async Task GetAvailabilityAsync_WhenTelnyxIsNotConfigured_ReportsUnknownWithoutCalling()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler();
        var provisioner = CreateProvisioner(handler, new TelnyxOptions());

        // Act
        var availability = await provisioner.GetAvailabilityAsync("cc-voice-media-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(VoiceMediaAvailability.Unknown, availability);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UploadAsync_PostsTheAudioUnderAUniqueNameWithThePurposePrefix()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().RespondWith(HttpStatusCode.OK, """{"data":{}}""");
        var provisioner = CreateProvisioner(handler);

        // Act
        var reference = await provisioner.UploadAsync(new MemoryStream(Encoding.UTF8.GetBytes("audio")), "audio/wav", "cc-vm-greeting", TestContext.Current.CancellationToken);

        // Assert
        Assert.StartsWith("cc-vm-greeting-", reference, StringComparison.Ordinal);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v2/media", request.Path);
        Assert.Contains(reference, request.Body, StringComparison.Ordinal);
    }

    private static TelnyxVoiceMediaProvisioner CreateProvisioner(HttpMessageHandler handler, TelnyxOptions options = null)
    {
        options ??= new TelnyxOptions
        {
            IsEnabled = true,
            ApiKey = "test-api-key",
            ConnectionId = "connection-1",
            ApiBaseUrl = "https://api.telnyx.com/v2/",
        };

        return new TelnyxVoiceMediaProvisioner(
            new StubHttpClientFactory(handler),
            new TestOptionsMonitor<TelnyxOptions>(options),
            NullLogger<TelnyxVoiceMediaProvisioner>.Instance);
    }
}
