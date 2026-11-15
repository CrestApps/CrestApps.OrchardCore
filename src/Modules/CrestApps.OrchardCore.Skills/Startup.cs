using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Skills.BackgroundTasks;
using CrestApps.OrchardCore.Skills.Core;
using CrestApps.OrchardCore.Skills.Core.Indexes;
using CrestApps.OrchardCore.Skills.Drivers;
using CrestApps.OrchardCore.Skills.Migrations;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Navigation;
using CrestApps.OrchardCore.Skills.Reports;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Skills.Webhooks;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Skills;

/// <summary>
/// Registers the skill catalog, certifications, the admin screens and the daily expiry check.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(SkillsConstants.CollectionName));
        services.AddDataMigration<SkillsMigrations>()
            .AddIndexProvider<SkillIndexProvider>()
            .AddIndexProvider<CertificationIndexProvider>()
            .AddIndexProvider<SkillAssignmentIndexProvider>()
            .AddIndexProvider<CertificationRecordIndexProvider>();

        services.AddSkillsCore();
        services.AddDisplayDriver<Skill, SkillRowDisplayDriver>();
        services.AddDisplayDriver<Certification, CertificationRowDisplayDriver>();
        services.AddDisplayDriver<Certification, CertificationDisplayDriver>();
        services.AddDisplayDriver<CertificationRecord, CertificationRecordRowDisplayDriver>();
        services.AddDisplayDriver<User, UserSkillsDisplayDriver>();
        services.AddPermissionProvider<SkillsPermissionsProvider>();
        services.AddPersonalDataContributor<SkillsPersonalDataContributor>();
        services.AddNavigationProvider<SkillsAdminMenu>();
        services.AddSingleton<IBackgroundTask, SkillsDailyBackgroundTask>();
    }
}

/// <summary>
/// Publishes issued, renewed and revoked certificates as webhook events.
/// </summary>
[RequireFeatures(WebhooksConstants.Feature.Webhooks)]
public sealed class SkillsWebhooksStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWebhookEvent(SkillsWebhookObserver.CertificateIssued, "Certificate issued", "Skills");
        services.AddWebhookEvent(SkillsWebhookObserver.CertificateRenewed, "Certificate renewed", "Skills");
        services.AddWebhookEvent(SkillsWebhookObserver.CertificateRevoked, "Certificate revoked", "Skills");
        services.AddScoped<ICertificationRecordObserver, SkillsWebhookObserver>();
    }
}

/// <summary>
/// Adds the Skills data source of Data Pipelines and the Report Builder.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class SkillsDataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataSource<SkillsReportDataSource>();
        services.AddScoped<ISkillsReportDataSet, CertificationsReportDataSet>();
        services.AddScoped<ISkillsReportDataSet, CertificatesReportDataSet>();
    }
}
