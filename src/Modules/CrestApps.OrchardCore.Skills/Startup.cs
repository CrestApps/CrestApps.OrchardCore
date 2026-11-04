using CrestApps.OrchardCore.Skills.BackgroundTasks;
using CrestApps.OrchardCore.Skills.Core;
using CrestApps.OrchardCore.Skills.Core.Indexes;
using CrestApps.OrchardCore.Skills.Drivers;
using CrestApps.OrchardCore.Skills.Migrations;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Navigation;
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
        services.AddDisplayDriver<CertificationRecord, CertificationRecordRowDisplayDriver>();
        services.AddDisplayDriver<User, UserSkillsDisplayDriver>();
        services.AddPermissionProvider<SkillsPermissionsProvider>();
        services.AddNavigationProvider<SkillsAdminMenu>();
        services.AddSingleton<IBackgroundTask, SkillsDailyBackgroundTask>();
    }
}
