using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the mandatory eligibility and suppression gate evaluated before outbound dialing attempts.
/// </summary>
[Feature(ContactCenterConstants.Feature.Dialer)]
public sealed class ComplianceStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplianceStartup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration used to bind outbound compliance options.</param>
    public ComplianceStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddOptions<ContactCenterComplianceOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps_ContactCenter:Compliance"))
            .Validate(
                options => options.AbandonmentRollingWindowMinutes is >= 1 and <= 1440,
                "The Contact Center abandonment rolling window must be between 1 and 1440 minutes.")
            .ValidateOnStart();

        services
            .AddOptions<ManualDialingComplianceOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps_ContactCenter:Compliance:ManualDialing"))
            .Validate(
                options => !options.EnforceCallingWindow || !string.IsNullOrWhiteSpace(options.CallingCalendarId),
                "Manual dialing calling-window enforcement requires a calling calendar id.")
            .ValidateOnStart();

        services
            .AddScoped<IDialerAbandonmentPolicyService, DefaultDialerAbandonmentPolicyService>()
            .AddScoped<IDialerEligibilityService, DefaultDialerEligibilityService>()
            .AddScoped<IProviderCommandDispatchValidator, DialerProviderCommandDispatchValidator>()
            .AddScoped<IDialerAttemptCompensationService, DialerAttemptCompensationService>()
            .AddScoped<IDialerAttemptService, DialerAttemptService>()
            .AddScoped<IOutboundCallScreener, ContactCenterManualCallScreener>();
    }
}
