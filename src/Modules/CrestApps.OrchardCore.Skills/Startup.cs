using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Skills.BackgroundTasks;
using CrestApps.OrchardCore.Skills.Core;
using CrestApps.OrchardCore.Skills.ContentTransfer;
using CrestApps.OrchardCore.Skills.Core.Indexes;
using CrestApps.OrchardCore.Skills.Drivers;
using CrestApps.OrchardCore.Skills.IssuedCertificates.Drivers;
using CrestApps.OrchardCore.Skills.IssuedCertificates.Migrations;
using CrestApps.OrchardCore.Skills.IssuedCertificates.Models;
using CrestApps.OrchardCore.Skills.IssuedCertificates.Services;
using CrestApps.OrchardCore.Skills.Migrations;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Navigation;
using CrestApps.OrchardCore.Skills.Reports;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Skills.Webhooks;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.RateLimits;
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
        services.AddUserAudienceRuleSource<SkillHolderAudienceRuleSource>();
        services.AddUserAudienceRuleSource<CertificationHolderAudienceRuleSource>();
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

/// <summary>
/// Prints issued certificates from templates and proves them on the public verification page.
/// </summary>
[Feature(SkillsConstants.Features.IssuedCertificates)]
public sealed class IssuedCertificatesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPdfDocumentRenderer();
        services.AddScoped<CertificateDocumentBuilder>();
        services.AddDataMigration<CertificateTemplateMigrations>();
        services.AddContentPart<CertificateTemplatePart>()
            .UseDisplayDriver<CertificateTemplatePartDisplayDriver>();
        services.AddDisplayDriver<Certification, IssuedCertificateSettingsDisplayDriver>();
        services.AddDisplayDriver<CertificationRecord, IssuedCertificateRecordDisplayDriver>();
    }
}

/// <summary>
/// Holds each visitor to a number of verification lookups a minute when Orchard Core's Rate Limits feature is on.
/// A code has 80 random bits, so guessing one is hopeless; the limit keeps the page from being used to probe.
/// </summary>
[Feature(SkillsConstants.Features.IssuedCertificates)]
[RequireFeatures(OrchardCoreFeatureIds.RateLimits)]
public sealed class IssuedCertificatesRateLimitsStartup : StartupBase
{
    /// <summary>
    /// How many verification pages a visitor may open a minute.
    /// </summary>
    public const int RequestsPerMinute = 30;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RateLimitsOptions>(options => options.AddGroupRateLimit(
            SkillsConstants.VerificationRateLimitGroup,
            RateLimitPartitionHelpers.CreateFixedWindowPerIpPolicy(
                policyName: SkillsConstants.VerificationRateLimitGroup,
                permitLimit: RequestsPerMinute,
                window: TimeSpan.FromMinutes(1))));
    }
}

/// <summary>
/// Adds the Skill holders report and its skill category filter.
/// </summary>
[RequireFeatures(ReportsConstants.Feature)]
public sealed class SkillsReportsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddReport<SkillHoldersReport>();
        services.AddDisplayDriver<ReportFilter, SkillCategoryReportFilterDisplayDriver>();
    }
}

/// <summary>
/// Lets skills and competency frameworks be imported from a file through ContentTransfer.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.ContentTransfer")]
public sealed class SkillsContentTransferStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentTransferImportTarget<SkillsImportTarget>();
    }
}
