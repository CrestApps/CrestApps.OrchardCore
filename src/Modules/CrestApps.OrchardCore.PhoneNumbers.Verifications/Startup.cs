using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.PhoneNumbers.Core;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Reports;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.BackgroundTasks;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Drivers;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Handlers;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Indexes;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Migrations;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;
using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications;

/// <summary>
/// Registers the core Phone Number Verifications services and infrastructure.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IPhoneNumberVerificationManager, DefaultPhoneNumberVerificationManager>();

        services.AddSiteDisplayDriver<PhoneNumberVerificationsSettingsDisplayDriver>();
        services.AddNavigationProvider<PhoneNumberVerificationsAdminMenu>();
        services.AddPermissionProvider<PhoneNumberVerificationsPermissionProvider>();
        services.AddScoped<IPhoneNumberVerificationQueueProcessor, PhoneNumberVerificationQueueProcessor>();
        services.AddSingleton<IPhoneNumberVerificationRequestDelayer, DefaultPhoneNumberVerificationRequestDelayer>();

        services.AddContentPart<PhoneNumberVerificationPart>()
            .UseDisplayDriver<PhoneNumberVerificationPartDisplayDriver>();

        services.AddIndexProvider<PhoneNumberVerificationPartIndexProvider>();
        services.AddDataMigration<PhoneNumberVerificationsMigrations>();

        services.AddSingleton<IBackgroundTask, PhoneNumberRevalidationBackgroundTask>();
    }
}

/// <summary>
/// Registers the shared Reports area integration for phone number verification reporting.
/// </summary>
[RequireFeatures(ReportsConstants.Feature)]
public sealed class ReportsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IReport, PhoneNumberVerificationReportProvider>();
    }
}

/// <summary>
/// Registers the AbstractAPI phone number verification provider.
/// </summary>
[Feature(PhoneNumberVerificationsConstants.Features.AbstractApi)]
public sealed class AbstractApiStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AbstractApiStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AbstractApiStartup(IStringLocalizer<AbstractApiStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(nameof(AbstractApiPhoneNumberVerificationProvider))
            .AddStandardResilienceHandler();

        services.AddPhoneNumberVerificationProvider<AbstractApiPhoneNumberVerificationProvider, AbstractApiPhoneNumberVerificationSettings>(
            PhoneNumberVerificationsConstants.Providers.AbstractApi,
            options =>
            {
                options.DisplayName = S["AbstractAPI"];
                options.Description = S["Verifies phone numbers using the AbstractAPI Phone Validation service."];
            });

        services.AddSiteDisplayDriver<AbstractApiPhoneNumberVerificationSettingsDisplayDriver>();
    }
}

/// <summary>
/// Registers the Veriphone phone number verification provider.
/// </summary>
[Feature(PhoneNumberVerificationsConstants.Features.Veriphone)]
public sealed class VeriphoneStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="VeriphoneStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public VeriphoneStartup(IStringLocalizer<VeriphoneStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(nameof(VeriphonePhoneNumberVerificationProvider))
            .AddStandardResilienceHandler();

        services.AddPhoneNumberVerificationProvider<VeriphonePhoneNumberVerificationProvider, VeriphonePhoneNumberVerificationSettings>(
            PhoneNumberVerificationsConstants.Providers.Veriphone,
            options =>
            {
                options.DisplayName = S["Veriphone"];
                options.Description = S["Verifies phone numbers using the Veriphone phone number validation service."];
            });

        services.AddSiteDisplayDriver<VeriphonePhoneNumberVerificationSettingsDisplayDriver>();
    }
}

/// <summary>
/// Registers the Twilio Lookup phone number verification provider.
/// </summary>
[Feature(PhoneNumberVerificationsConstants.Features.Twilio)]
public sealed class TwilioStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TwilioStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TwilioStartup(IStringLocalizer<TwilioStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(nameof(TwilioPhoneNumberVerificationProvider))
            .AddStandardResilienceHandler();

        services.AddPhoneNumberVerificationProvider<TwilioPhoneNumberVerificationProvider, TwilioPhoneNumberVerificationSettings>(
            PhoneNumberVerificationsConstants.Providers.Twilio,
            options =>
            {
                options.DisplayName = S["Twilio"];
                options.Description = S["Verifies phone numbers using the Twilio Lookup service."];
            });

        services.AddSiteDisplayDriver<TwilioPhoneNumberVerificationSettingsDisplayDriver>();
    }
}

/// <summary>
/// Registers automatic verification for Omnichannel contact content items.
/// </summary>
[RequireFeatures(OmnichannelConstants.Features.Managements)]
public sealed class OmnichannelContactVerificationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentHandler, OmnichannelContactPhoneNumberVerificationHandler>();
    }
}
