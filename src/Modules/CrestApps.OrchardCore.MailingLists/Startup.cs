using CrestApps.Core.Services;
using CrestApps.OrchardCore.MailingLists.BackgroundTasks;
using CrestApps.OrchardCore.MailingLists.Core;
using CrestApps.OrchardCore.MailingLists.Core.Indexes;
using CrestApps.OrchardCore.MailingLists.Core.Services;
using CrestApps.OrchardCore.MailingLists.Drivers;
using CrestApps.OrchardCore.MailingLists.Migrations;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Navigation;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.MailingLists;

/// <summary>
/// Registers the Mailing Lists feature: the lists and members, their admin pages, the verification page, the e-mails
/// and their settings, the provider outbox and reconciliation, the consent source, the unsubscribe scope, personal data
/// and the Outreach audience.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The tenant's configuration, for <see cref="MailingListsOptions"/>.</param>
    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(MailingListsConstants.CollectionName));
        services.Configure<MailingListsOptions>(_shellConfiguration.GetSection(MailingListsOptions.SectionName));

        services.AddMailingListsCore();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<MailingList>, MailingListHandler>());
        services.AddIndexProvider<MailingListIndexProvider>();
        services.AddIndexProvider<MailingListMemberIndexProvider>();
        services.AddDataMigration<MailingListsMigrations>();

        services.AddPermissionProvider<MailingListsPermissionsProvider>();
        services.AddNavigationProvider<MailingListsAdminMenu>();
        services.AddDisplayDriver<MailingList, MailingListDisplayDriver>();
        services.AddDisplayDriver<MailingList, MailingListProviderDisplayDriver>();
        services.AddDisplayDriver<MailingListMember, MailingListMemberDisplayDriver>();

        services
            .AddTemplatedEmailKind(MailingListsConstants.EmailGroup, MailingListsConstants.EmailKinds.Confirm, LocalizationSource.Create<Startup>("Confirm a sign-up"), LocalizationSource.Create<Startup>("{{ Links.Verify }}, {{ Coupon.Code }} when the code is given at sign-up"))
            .AddTemplatedEmailKind(MailingListsConstants.EmailGroup, MailingListsConstants.EmailKinds.Welcome, LocalizationSource.Create<Startup>("Welcome"), LocalizationSource.Create<Startup>("{{ Coupon.Code }}, {{ Coupon.Url }}, {{ Coupon.Description }}, {{ Coupon.Expires }} when a welcome coupon was given"))
            .AddTemplatedEmailKind(MailingListsConstants.EmailGroup, MailingListsConstants.EmailKinds.CouponClaim, LocalizationSource.Create<Startup>("Claim the welcome coupon"), LocalizationSource.Create<Startup>("{{ Links.Verify }}"));
        services.AddTemplatedEmailTemplateSource<MailingListEmailTemplateSource>();
        services.AddSiteDisplayDriver<MailingListEmailSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(MailingListsConstants.SettingsGroupId, MailingListsPermissions.ManageMailingListSettings);

        services.AddRecordSource<MailingListMemberRecordSource>();
        services.AddPersonalDataContributor<MailingListPersonalDataContributor>();

        // Outreach reads the subscribed members of a list when a campaign sends to it.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutreachAudienceSource, MailingListAudienceSource>());

        services.AddSingleton<IBackgroundTask, MailingListSyncBackgroundTask>();
        services.AddSingleton<IBackgroundTask, MailingListReconciliationBackgroundTask>();
    }
}

/// <summary>
/// Writes every member change on the member's timeline when the Timeline feature is on.
/// </summary>
[RequireFeatures(TimelineConstants.Features.Timeline)]
public sealed class TimelineStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMailingListMemberObserver<MailingListTimelineObserver>();
        services.AddTimelineAccessProvider<MailingListMemberTimelineAccess>();
    }
}
