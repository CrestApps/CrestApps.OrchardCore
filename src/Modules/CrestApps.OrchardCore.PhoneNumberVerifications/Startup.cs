using CrestApps.OrchardCore.PhoneNumberVerifications.BackgroundTasks;
using CrestApps.OrchardCore.PhoneNumberVerifications.Drivers;
using CrestApps.OrchardCore.PhoneNumberVerifications.Indexes;
using CrestApps.OrchardCore.PhoneNumberVerifications.Migrations;
using CrestApps.OrchardCore.PhoneNumberVerifications.Models;
using CrestApps.OrchardCore.PhoneNumberVerifications.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.PhoneNumberVerifications;

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

        services.AddContentPart<PhoneNumberVerificationPart>()
            .UseDisplayDriver<PhoneNumberVerificationPartDisplayDriver>();

        services.AddIndexProvider<PhoneNumberVerificationPartIndexProvider>();
        services.AddDataMigration<PhoneNumberVerificationsMigrations>();

        services.AddSingleton<IBackgroundTask, PhoneNumberRevalidationBackgroundTask>();
    }
}

/// <summary>
/// Registers the AbstractAPI phone number verification provider.
/// </summary>
[Feature(PhoneNumberVerificationsConstants.Features.AbstractApi)]
public sealed class AbstractApiStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(nameof(AbstractApiPhoneNumberVerificationProvider));

        services.AddPhoneNumberVerificationProvider<AbstractApiPhoneNumberVerificationProvider>(
            PhoneNumberVerificationsConstants.Providers.AbstractApi,
            "AbstractAPI",
            "Verifies phone numbers using the AbstractAPI Phone Validation service.");

        services.AddSiteDisplayDriver<AbstractApiPhoneNumberVerificationSettingsDisplayDriver>();
    }
}
