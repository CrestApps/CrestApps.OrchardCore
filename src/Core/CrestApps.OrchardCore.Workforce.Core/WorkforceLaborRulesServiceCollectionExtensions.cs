using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Core.Handlers;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Core.Services.LaborRules;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Workforce.Core;

/// <summary>
/// Registers the labor rule services.
/// </summary>
public static class WorkforceLaborRulesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the built-in labor rules, rule sets, the evaluator, the checks on publishing and on taking shifts,
    /// overrides, predictability pay, and the limits rule sets give the auto-scheduler. Call it with
    /// <see cref="WorkforceCoreServiceCollectionExtensions.AddWorkforceCore"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddWorkforceLaborRulesCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<LaborRuleSetStore>();
        services.TryAddScoped<LaborRuleOverrideStore>();
        services.TryAddScoped<PredictabilityPayStore>();
        services.TryAddScoped<ILaborRuleSetManager, LaborRuleSetManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<LaborRuleSet>, LaborRuleSetCatalogHandler>());
        services.TryAddScoped<ILaborRuleEvaluator, DefaultLaborRuleEvaluator>();
        services.TryAddScoped<ILaborRuleOverrideService, DefaultLaborRuleOverrideService>();
        services.TryAddScoped<IPredictabilityPayService, DefaultPredictabilityPayService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISchedulePublishValidator, LaborRulesPublishValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IScheduleChangeHandler, PredictabilityPayChangeHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IShiftAssignmentValidator, LaborRuleShiftAssignmentValidator>());
        services.Replace(ServiceDescriptor.Scoped<IWorkerScheduleLimitsProvider, RuleSetWorkerScheduleLimitsProvider>());

        services.AddLaborRule<MaxHoursPerDayRule>()
            .AddLaborRule<MaxHoursPerWeekRule>()
            .AddLaborRule<MinHoursPerWeekRule>()
            .AddLaborRule<MinRestRule>()
            .AddLaborRule<MaxConsecutiveDaysRule>()
            .AddLaborRule<DaysOffPerWeekRule>()
            .AddLaborRule<MaxShiftLengthRule>()
            .AddLaborRule<MinShiftLengthRule>()
            .AddLaborRule<BreaksRule>()
            .AddLaborRule<MinorLimitsRule>()
            .AddLaborRule<SkillsRequiredRule>()
            .AddLaborRule<NoWorkDuringTimeOffRule>()
            .AddLaborRule<ScheduleNoticeRule>()
            .AddLaborRule<OvertimeRule>()
            .AddLaborRule<PredictabilityPayRule>();

        return services;
    }
}
