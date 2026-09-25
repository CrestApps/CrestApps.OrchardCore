using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A real <see cref="TelnyxContactCenterVoiceProvider"/> whose Call Control requests go to a recording handler.
/// </summary>
internal static class TelnyxContactCenterProviderFactory
{
    public static TelnyxContactCenterVoiceProvider Create(HttpMessageHandler handler, ITelnyxAgentEndpointResolver resolver = null)
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
            resolver ?? new Mock<ITelnyxAgentEndpointResolver>().Object,
            apiClient,
            new Mock<IClock>().Object,
            NullLogger<TelnyxContactCenterVoiceProvider>.Instance,
            monitor.Object,
            localizer.Object);
    }
}
