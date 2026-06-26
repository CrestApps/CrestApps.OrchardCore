using CrestApps.OrchardCore.PhoneNumberVerifications.Drivers;
using CrestApps.OrchardCore.PhoneNumberVerifications.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.PhoneNumberVerifications;

/// <summary>
/// Registers the Twilio Lookup phone number verification provider.
/// </summary>
[Feature(PhoneNumberVerificationsConstants.Features.Twilio)]
public sealed class TwilioStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(nameof(TwilioPhoneNumberVerificationProvider));

        services.AddPhoneNumberVerificationProvider<TwilioPhoneNumberVerificationProvider>(
            PhoneNumberVerificationsConstants.Providers.Twilio,
            "Twilio",
            "Verifies phone numbers using the Twilio Lookup service.");

        services.AddSiteDisplayDriver<TwilioPhoneNumberVerificationSettingsDisplayDriver>();
    }
}
