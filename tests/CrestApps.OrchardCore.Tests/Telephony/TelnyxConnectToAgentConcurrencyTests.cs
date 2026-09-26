using System.Net;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Accepting an offer answered the caller's leg and only then dialled the agent, so the answer's round trip was added
/// to the time the agent waited after clicking Answer. The bridge waits for the agent's leg anyway, so the two must be
/// sent together. The caller's answer here does not complete until the agent's dial has been sent: sequential code
/// never sends it and times out.
/// </summary>
public sealed class TelnyxConnectToAgentConcurrencyTests
{
    [Fact]
    public async Task ConnectToAgentAsync_DialsTheAgentWithoutWaitingForTheCallersAnswer()
    {
        // Arrange
        var handler = new AnswerWaitsForDialHandler();
        var provider = CreateProvider(handler);

        // Act
        var result = await provider.ConnectToAgentAsync(new ContactCenterConnectRequest
        {
            ProviderCallId = "caller-1",
            AgentUserId = "u1",
            AgentEndpoint = "sip:agent@sip.telnyx.test",
        }, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(handler.DialSentBeforeAnswerReturned);
    }

    private static TelnyxContactCenterVoiceProvider CreateProvider(HttpMessageHandler handler)
    {
        var options = new TelnyxOptions
        {
            IsEnabled = true,
            ApiKey = "KEY",
            ConnectionId = "connection-1",
            ApiBaseUrl = "https://api.telnyx.test/v2/",
            DefaultOutboundCallerId = "+15550000000",
        };

        var monitor = new Mock<IOptionsMonitor<TelnyxOptions>>();
        monitor.SetupGet(value => value.CurrentValue).Returns(options);

        var workManager = new Mock<IContactCenterFeatureWorkManager>();
        workManager
            .Setup(value => value.TryEnter(It.IsAny<string>()))
            .Returns(new Mock<IContactCenterFeatureWorkLease>().Object);

        var localizer = new Mock<IStringLocalizer<TelnyxContactCenterVoiceProvider>>();
        localizer.Setup(value => value[It.IsAny<string>()]).Returns<string>(name => new LocalizedString(name, name));

        var apiClient = new TelnyxApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.telnyx.test/v2/") },
            new OptionsWrapper<TelnyxOptions>(options),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);

        return new TelnyxContactCenterVoiceProvider(
            new Mock<ITelephonyProviderResolver>().Object,
            workManager.Object,
            new Mock<ITelnyxAgentCredentialStore>().Object,
            new Mock<ITelnyxAgentEndpointResolver>().Object,
            apiClient,
            new Mock<IClock>().Object,
            NullLogger<TelnyxContactCenterVoiceProvider>.Instance,
            monitor.Object,
            localizer.Object);
    }

    // Holds the caller's answer open until the agent's dial arrives.
    private sealed class AnswerWaitsForDialHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _dialSent = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool DialSentBeforeAnswerReturned { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri.AbsolutePath;

            if (path.EndsWith("/actions/answer", StringComparison.Ordinal))
            {
                await _dialSent.Task.WaitAsync(cancellationToken);
                DialSentBeforeAnswerReturned = true;

                return Json("{\"data\":{}}");
            }

            if (path.EndsWith("/calls", StringComparison.Ordinal))
            {
                _dialSent.TrySetResult();

                return Json("{\"data\":{\"call_control_id\":\"agent-leg-1\"}}");
            }

            return Json("{\"data\":{}}");
        }

        private static HttpResponseMessage Json(string body)
            => new(HttpStatusCode.OK) { Content = new StringContent(body) };
    }
}
