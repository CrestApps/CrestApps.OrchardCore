using CrestApps.Core.Services;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Workforce.Core.Handlers;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Workforce.Core;

/// <summary>
/// Registers the Workforce services and the Workforce calendar type.
/// </summary>
public static class WorkforceCoreServiceCollectionExtensions
{
    private static readonly Type _source = typeof(WorkforceCoreServiceCollectionExtensions);

    /// <summary>
    /// Registers the worker, team and activity code stores and managers, manager scoping, team calendars, calendar
    /// access by team, the busy time of breaks inside shifts, shift templates, rotations, coverage, schedule planning and
    /// publishing, the built-in schedule solver, open shifts, shift trades and the checks they run, and the Workforce
    /// calendar type.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddWorkforceCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<WorkerStore>();
        services.TryAddScoped<TeamStore>();
        services.TryAddScoped<ActivityCodeStore>();
        services.TryAddScoped<IWorkerManager, WorkerManager>();
        services.TryAddScoped<ITeamManager, TeamManager>();
        services.TryAddScoped<IActivityCodeManager, ActivityCodeManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<WorkerProfile>, WorkerCatalogHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<WorkforceTeam>, TeamCatalogHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<ActivityCode>, ActivityCodeCatalogHandler>());
        services.TryAddScoped<IWorkforceScopeService, DefaultWorkforceScopeService>();
        services.TryAddScoped<WorkforceCalendarService>();
        services.TryAddScoped<ActivityCodeSeeder>();
        services.AddCalendarAccessContributor<WorkforceCalendarAccessContributor>();
        services.AddCalendarBusyTimeSource<ShiftSegmentBusyTimeSource>();

        services.TryAddScoped<TeamTimeZoneResolver>();
        services.TryAddScoped<ShiftTemplateStore>();
        services.TryAddScoped<ShiftPatternStore>();
        services.TryAddScoped<CoverageRequirementStore>();
        services.TryAddScoped<SchedulePeriodStore>();
        services.TryAddScoped<ScheduleScenarioStore>();
        services.TryAddScoped<DraftShiftStore>();
        services.TryAddScoped<ScheduleChangeStore>();
        services.TryAddScoped<IShiftTemplateManager, ShiftTemplateManager>();
        services.TryAddScoped<IShiftPatternManager, ShiftPatternManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<ShiftTemplate>, ShiftTemplateCatalogHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<ShiftPattern>, ShiftPatternCatalogHandler>());
        services.TryAddScoped<ICoverageService, DefaultCoverageService>();
        services.TryAddScoped<ISchedulePlanningService, DefaultSchedulePlanningService>();

        services.AddOptions<ShiftPlanningOptions>();
        services.TryAddScoped<OpenShiftStore>();
        services.TryAddScoped<ShiftTradeStore>();
        services.TryAddScoped<WorkerShiftReader>();
        services.TryAddScoped<WorkerLocks>();
        services.TryAddScoped<TeamPlanningPolicy>();
        services.TryAddScoped<ShiftAssignmentContexts>();
        services.TryAddScoped<OpenShiftPosting>();
        services.TryAddScoped<ScheduleAutoScheduler>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IShiftScheduleSolver, DefaultShiftScheduleSolver>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IShiftAssignmentValidator, DefaultShiftAssignmentValidator>());
        services.TryAddScoped<IShiftService, DefaultShiftService>();
        services.TryAddScoped<IOpenShiftService, DefaultOpenShiftService>();
        services.TryAddScoped<IShiftTradeService, DefaultShiftTradeService>();

        services.AddCalendarType(WorkforceConstants.CalendarType, type => type
            .WithDisplayName(LocalizationSource.Create("Workforce", _source))
            .WithDescription(LocalizationSource.Create("A team's published shifts, approved time off, training and meetings. Workforce creates one for each team.", _source))
            .WithIcon("fa-solid fa-user-clock")
            .WithColor("#2b8a3e")
            .WithOwner(CalendarOwnerKind.None)
            .WithPosition(60)
            .ManagedByCode()
            .AddEventType(WorkforceConstants.EventTypes.Shift, LocalizationSource.Create("Shift", _source), e => e.Color("#2b8a3e").Free().IgnoresCapacity())
            .AddEventType(WorkforceConstants.EventTypes.TimeOff, LocalizationSource.Create("Time off", _source), e => e.Color("#e8590c").Busy().IgnoresCapacity().AllDayByDefault())
            .AddEventType(WorkforceConstants.EventTypes.Training, LocalizationSource.Create("Training", _source), e => e.Color("#1971c2").Busy())
            .AddEventType(WorkforceConstants.EventTypes.Meeting, LocalizationSource.Create("Meeting", _source), e => e.Color("#6741d9").Busy())
            .AddEventType(WorkforceConstants.EventTypes.OnCall, LocalizationSource.Create("On call", _source), e => e.Color("#f08c00").Free().RequiresAttendee()));

        return services;
    }
}
