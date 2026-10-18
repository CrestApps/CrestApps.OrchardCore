using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// A dialer call asks Telnyx to screen for answering machines only when its profile asks, in Telnyx's own words, and the
/// Contact Center is told the call is screened so it holds the agent for the verdict, and only then.
/// </summary>
public sealed class TelnyxDialerScreeningDialBodyTests
{
    [Theory]
    [InlineData("standard", "detect")]
    [InlineData("premium", "premium")]
    [InlineData("Premium ", "premium")]
    public async Task DialAsync_WhenTheDialerAsksForScreening_SendsTelnyxsDetectionMode(string requested, string expected)
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{\"call_control_id\":\"customer-1\"}}");
        var provider = CreateTelephonyProvider(handler);

        // Act
        var result = await provider.DialAsync(DialerDial(requested), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        using var body = JsonDocument.Parse(handler.LastRequestBody);
        Assert.Equal(expected, body.RootElement.GetProperty("answering_machine_detection").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("disabled")]
    public async Task DialAsync_WhenTheDialerDoesNotAskForScreening_SendsNoDetection(string requested)
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{\"call_control_id\":\"customer-1\"}}");
        var provider = CreateTelephonyProvider(handler);

        // Act
        var result = await provider.DialAsync(DialerDial(requested), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        using var body = JsonDocument.Parse(handler.LastRequestBody);
        Assert.False(body.RootElement.TryGetProperty("answering_machine_detection", out _));
    }

    [Theory]
    [InlineData("standard", true)]
    [InlineData("premium", true)]
    [InlineData(null, false)]
    [InlineData("disabled", false)]
    public async Task ContactCenterDialAsync_SaysTheCallIsScreened_OnlyWhenScreeningWasAskedFor(string requested, bool screened)
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{\"call_control_id\":\"customer-1\"}}");
        var telephony = CreateTelephonyProvider(handler);
        var resolver = new Mock<ITelephonyProviderResolver>();
        resolver.Setup(value => value.GetAsync(It.IsAny<string>())).ReturnsAsync(telephony);
        var provider = CreateContactCenterProvider(handler, resolver.Object);

        var request = new ContactCenterDialRequest
        {
            Destination = "+15551230001",
            CallerId = "+15550000000",
            Metadata = new Dictionary<string, string>(),
        };

        if (requested is not null)
        {
            request.Metadata[TelephonyConstants.RequestMetadata.AnsweringMachineDetection] = requested;
        }

        // Act
        var result = await provider.DialAsync(request, TestContext.Current.CancellationToken);

        // Assert: a screened call holds its agent for the verdict; any other would wait for one that never comes.
        Assert.True(result.Succeeded);
        Assert.Equal(screened, result.Metadata.ContainsKey(ContactCenterConstants.TelephonyMetadata.AnswerDetectionRequested));
    }

    private static DialRequest DialerDial(string answeringMachineDetection)
    {
        var request = new DialRequest
        {
            To = "+15551230001",
            Metadata = new Dictionary<string, string>(),
        };

        if (answeringMachineDetection is not null)
        {
            request.Metadata[TelephonyConstants.RequestMetadata.AnsweringMachineDetection] = answeringMachineDetection;
        }

        return request;
    }

    private static TelnyxOptions CreateOptions()
        => new()
        {
            IsEnabled = true,
            ApiKey = "KEY",
            ConnectionId = "connection-1",
            ApiBaseUrl = "https://api.telnyx.test/v2/",
            DefaultOutboundCallerId = "+15550000000",
        };

    private static TelnyxApiClient CreateApiClient(StubHttpMessageHandler handler)
        => new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.telnyx.test/v2/") },
            new TestOptionsMonitor<TelnyxOptions>(CreateOptions()),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);

    private static TelnyxTelephonyProvider CreateTelephonyProvider(StubHttpMessageHandler handler)
        => new(
            CreateApiClient(handler),
            new Mock<ITelnyxAgentCredentialStore>().Object,
            new Mock<ITelnyxAgentEndpointResolver>().Object,
            new StubClock(),
            NullLogger<TelnyxTelephonyProvider>.Instance,
            new PassThroughStringLocalizer<TelnyxTelephonyProvider>(),
            new TestOptionsMonitor<TelnyxOptions>(CreateOptions()));

    private static TelnyxContactCenterVoiceProvider CreateContactCenterProvider(StubHttpMessageHandler handler, ITelephonyProviderResolver resolver)
    {
        var workManager = new Mock<IContactCenterFeatureWorkManager>();
        workManager
            .Setup(value => value.TryEnter(It.IsAny<string>()))
            .Returns(new Mock<IContactCenterFeatureWorkLease>().Object);

        var localizer = new Mock<IStringLocalizer<TelnyxContactCenterVoiceProvider>>();
        localizer.Setup(value => value[It.IsAny<string>()]).Returns<string>(name => new LocalizedString(name, name));

        return new TelnyxContactCenterVoiceProvider(
            resolver,
            workManager.Object,
            new Mock<ITelnyxAgentCredentialStore>().Object,
            new Mock<ITelnyxAgentEndpointResolver>().Object,
            CreateApiClient(handler),
            new StubClock(),
            NullLogger<TelnyxContactCenterVoiceProvider>.Instance,
            new TestOptionsMonitor<TelnyxOptions>(CreateOptions()),
            localizer.Object);
    }
}
