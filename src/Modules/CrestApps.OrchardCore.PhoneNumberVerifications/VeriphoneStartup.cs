using CrestApps.OrchardCore.PhoneNumberVerifications.Drivers;
using CrestApps.OrchardCore.PhoneNumberVerifications.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.PhoneNumberVerifications;

/// <summary>
/// Registers the Veriphone phone number verification provider.
/// </summary>
[Feature(PhoneNumberVerificationsConstants.Features.Veriphone)]
public sealed class VeriphoneStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(nameof(VeriphonePhoneNumberVerificationProvider));

        services.AddPhoneNumberVerificationProvider<VeriphonePhoneNumberVerificationProvider>(
            PhoneNumberVerificationsConstants.Providers.Veriphone,
            "Veriphone",
            "Verifies phone numbers using the Veriphone phone number validation service.");

        services.AddSiteDisplayDriver<VeriphonePhoneNumberVerificationSettingsDisplayDriver>();
    }
}
