using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Drivers;
using CrestApps.OrchardCore.Workforce.Migrations;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Navigation;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Registers shift templates, rotations, coverage and team schedules, and their admin screens.
/// </summary>
[Feature(WorkforceConstants.Features.ShiftPlanning)]
public sealed class ShiftPlanningStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<ShiftPlanningMigrations>()
            .AddIndexProvider<ShiftTemplateIndexProvider>()
            .AddIndexProvider<ShiftPatternIndexProvider>()
            .AddIndexProvider<CoverageRequirementIndexProvider>()
            .AddIndexProvider<SchedulePeriodIndexProvider>()
            .AddIndexProvider<ScheduleScenarioIndexProvider>()
            .AddIndexProvider<DraftShiftIndexProvider>()
            .AddIndexProvider<ScheduleChangeIndexProvider>();

        services.AddDisplayDriver<ShiftTemplate, ShiftTemplateRowDisplayDriver>();
        services.AddDisplayDriver<ShiftPattern, ShiftPatternRowDisplayDriver>();
        services.AddDisplayDriver<CoverageRequirement, CoverageRequirementRowDisplayDriver>();
        services.AddDisplayDriver<SchedulePeriod, SchedulePeriodRowDisplayDriver>();
        services.AddDisplayDriver<ScheduleChangeRowViewModel, ScheduleChangeRowDisplayDriver>();
        services.AddPermissionProvider<ShiftPlanningPermissionsProvider>();
        services.AddNavigationProvider<ShiftPlanningAdminMenu>();
    }
}
