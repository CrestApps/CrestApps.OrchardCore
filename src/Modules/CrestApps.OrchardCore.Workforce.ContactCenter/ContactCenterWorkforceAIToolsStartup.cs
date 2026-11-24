using CrestApps.Core.AI;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Workforce.ContactCenter.AI;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.ContactCenter;

/// <summary>
/// Adds the contact center workforce AI tool that gives every agent a worker. It works only for a signed-in person with
/// the Contact Center Agents page's permission.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.ContactCenter)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class ContactCenterWorkersAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterWorkersAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterWorkersAIToolsStartup(IStringLocalizer<ContactCenterWorkersAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<CreateWorkersForAgentsTool>(CreateWorkersForAgentsTool.ToolName)
            .WithTitle(S["Create workers for agents"])
            .WithDescription(S["Creates a worker profile for every contact center agent that has none, optionally in a team, and returns how many were created. Use it after turning Workforce on for a contact center that already has agents."])
            .WithCategory(S["Workforce"])
            .Selectable();
    }
}

/// <summary>
/// Adds the AI tools that read and set the Time Tracker stage of agent states and reasons. They work only for a signed-in
/// person with the Contact Center Stages page's permission.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.TimeTracker)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class ContactCenterTimeStagesAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterTimeStagesAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterTimeStagesAIToolsStartup(IStringLocalizer<ContactCenterTimeStagesAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetContactCenterTimeStagesTool>(GetContactCenterTimeStagesTool.ToolName)
            .WithTitle(S["Get contact center time stages"])
            .WithDescription(S["Gets which Time Tracker stage each contact center agent state (available, on a call, wrap-up...) and each not-ready reason is recorded in."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<SaveContactCenterTimeStagesTool>(SaveContactCenterTimeStagesTool.ToolName)
            .WithTitle(S["Save contact center time stages"])
            .WithDescription(S["Sets which Time Tracker stage each agent state and each not-ready reason is recorded in. Only existing stage ids are accepted."])
            .WithCategory(S["Workforce"])
            .Selectable();
    }
}

/// <summary>
/// Adds the adherence AI tools (an agent's day and adherence exceptions) and the read-only <c>adherenceRuleSet</c>
/// record kind. They work only for a signed-in person with the adherence pages' permission, for the workers of the
/// teams that person manages.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.Adherence)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AdherenceAdminAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdherenceAdminAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdherenceAdminAIToolsStartup(IStringLocalizer<AdherenceAdminAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetAdherenceDayTool>(GetAdherenceDayTool.ToolName)
            .WithTitle(S["Get adherence day"])
            .WithDescription(S["Gets an agent's adherence for a day: adherence and conformance percent, minutes in and out, each stretch out of adherence with the scheduled activity and the actual state, and the exceptions applied."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<ListAdherenceExceptionsTool>(ListAdherenceExceptionsTool.ToolName)
            .WithTitle(S["List adherence exceptions"])
            .WithDescription(S["Lists the adherence exceptions recorded for a team or a worker and date range, with reason, note and who recorded each."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<AddAdherenceExceptionTool>(AddAdherenceExceptionTool.ToolName)
            .WithTitle(S["Add adherence exception"])
            .WithDescription(S["Records an adherence exception for an agent for a time range, with a reason and note, so that time does not count against them, and can recalculate the day."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<DeleteAdherenceExceptionTool>(DeleteAdherenceExceptionTool.ToolName)
            .WithTitle(S["Delete adherence exception"])
            .WithDescription(S["Deletes an adherence exception, so the time counts against the agent again."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddAIRecordKind<AdherenceRuleSet>("adherenceRuleSet", kind => kind
            .WithTitle(S["Adherence rule set"])
            .WithDescription(S["an adherence rule set: for the teams it names, which agent states and reasons count as adherent during each scheduled activity, with start and end grace, the minimum minutes out and when to alert. Read-only here; it is changed on the Adherence Rule Sets page"])
            .WithPermission(WorkforcePermissions.ManageAdherence)
            .UseCatalogManager<AdherenceRuleSetManager>()
            .ListUsing(ContactCenterRecordKinds.ListByName<AdherenceRuleSet, AdherenceRuleSetManager>(set => set.Name))
            .WithProjection(set => new
            {
                id = set.ItemId,
                name = set.Name,
                teamIds = set.TeamIds.ToArray(),
                rules = set.Rules.Select(rule => new
                {
                    activity = rule.Kind.ToString(),
                    activityCodeId = rule.ActivityCodeId,
                    allowedStates = rule.AllowedStatuses.Select(status => status.ToString()).ToArray(),
                    allowedReasons = rule.AllowedReasons.ToArray(),
                    startGraceMinutes = rule.StartGraceMinutes,
                    endGraceMinutes = rule.EndGraceMinutes,
                    minimumOutMinutes = rule.MinimumOutMinutes,
                    alertAfterMinutes = rule.AlertAfterMinutes,
                }).ToArray(),
            })
            .ReadOnly());
    }
}

/// <summary>
/// Adds the intraday AI tools: the day as it happens, the reforecast, and voluntary overtime and time-off offers. They
/// work only for a signed-in person with the Intraday page's permission.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.Intraday)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class IntradayAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntradayAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public IntradayAIToolsStartup(IStringLocalizer<IntradayAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetIntradayStatusTool>(GetIntradayStatusTool.ToolName)
            .WithTitle(S["Get intraday status"])
            .WithDescription(S["For a planning group and day, compares forecast with actual contacts and handle time and agents needed with scheduled and available per interval, and lists the open overtime and time-off offers."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<ReforecastIntradayTool>(ReforecastIntradayTool.ToolName)
            .WithTitle(S["Reforecast intraday"])
            .WithDescription(S["Rescales the rest of today's forecast for a planning group from how the day has gone so far, and returns the factor used."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<PostIntradayOfferTool>(PostIntradayOfferTool.ToolName)
            .WithTitle(S["Post intraday offer"])
            .WithDescription(S["Offers voluntary overtime or voluntary time off to a team for a time range, for a number of places. Workers see it in My Work and take a place until none are left."])
            .WithCategory(S["Workforce"])
            .Selectable()
            .RequireApproval();

        services.AddCoreAITool<CloseIntradayOfferTool>(CloseIntradayOfferTool.ToolName)
            .WithTitle(S["Close intraday offer"])
            .WithDescription(S["Closes an overtime or time-off offer so no more workers can take it. Places already taken stay."])
            .WithCategory(S["Workforce"])
            .Selectable();
    }
}

/// <summary>
/// Adds the forecasting AI reads (forecasts and capacity plan projections) and the read-only <c>planningGroup</c>,
/// <c>forecastEvent</c> and <c>capacityPlan</c> record kinds. They work only for a signed-in person with the Forecasting
/// pages' permission. Building, adjusting, activating and deleting forecasts stay on the Forecasts page.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.Forecasting)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class ForecastingAdminAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForecastingAdminAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ForecastingAdminAIToolsStartup(IStringLocalizer<ForecastingAdminAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListForecastsTool>(ListForecastsTool.ToolName)
            .WithTitle(S["List forecasts"])
            .WithDescription(S["Lists the forecasts of a planning group or of all, with their dates, model, status (draft or active) and accuracy."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<GetForecastTool>(GetForecastTool.ToolName)
            .WithTitle(S["Get forecast"])
            .WithDescription(S["Gets a forecast: its adjustments, its accuracy against history and, for a chosen day, each interval's contacts, handle time and agents needed."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<GetCapacityPlanProjectionTool>(GetCapacityPlanProjectionTool.ToolName)
            .WithTitle(S["Get capacity plan projection"])
            .WithDescription(S["Gives a capacity plan's week-by-week projection: the workload, the full-time agents needed, those available and those in training, and when the plan runs short."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddAIRecordKind<PlanningGroup>("planningGroup", kind => kind
            .WithTitle(S["Planning group"])
            .WithDescription(S["a forecasting planning group: the contact center queues and channel it forecasts, the team that staffs it, its service goal (percent answered in seconds, or average speed of answer), maximum occupancy, concurrency, patience, shrinkage and weeks of history. Read-only here; it is changed on the Planning Groups page"])
            .WithPermission(WorkforcePermissions.ManageForecasts)
            .UseCatalogManager<PlanningGroupManager>()
            .ListUsing(ContactCenterRecordKinds.ListByName<PlanningGroup, PlanningGroupManager>(group => group.Name))
            .WithProjection(group => new
            {
                id = group.ItemId,
                name = group.Name,
                queueIds = group.QueueIds.ToArray(),
                channel = group.Channel?.ToString(),
                teamId = group.TeamId,
                serviceLevelPercent = group.ServiceLevelPercent,
                serviceLevelSeconds = group.ServiceLevelSeconds,
                averageSpeedOfAnswerSeconds = group.AverageSpeedOfAnswerSeconds,
                maxOccupancyPercent = group.MaxOccupancyPercent,
                concurrency = group.Concurrency,
                patienceSeconds = group.PatienceSeconds,
                shrinkagePercent = group.ShrinkagePercent,
                historyWeeks = group.HistoryWeeks,
            })
            .ReadOnly());

        services.AddAIRecordKind<ForecastEvent>("forecastEvent", kind => kind
            .WithTitle(S["Forecast event"])
            .WithDescription(S["a forecast event: a named date range (holiday, closure, promotion, outage...) that multiplies the forecast volume of planning groups, once or every year. Read-only here; it is changed on the Forecast Events page"])
            .WithPermission(WorkforcePermissions.ManageForecasts)
            .UseCatalogManager<ForecastEventManager>()
            .ListUsing(ContactCenterRecordKinds.ListByName<ForecastEvent, ForecastEventManager>(item => item.Name))
            .WithProjection(item => new
            {
                id = item.ItemId,
                name = item.Name,
                kind = item.Kind.ToString(),

                // Pure dates, shown as entered.
                startDate = item.StartDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                endDate = item.EndDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                volumeMultiplier = item.VolumeMultiplier,
                recursYearly = item.RecursYearly,
                planningGroupIds = item.PlanningGroupIds.ToArray(),
            })
            .ReadOnly());

        services.AddAIRecordKind<CapacityPlan>("capacityPlan", kind => kind
            .WithTitle(S["Capacity plan"])
            .WithDescription(S["a long-range capacity plan of a planning group: start, weeks, weekly workload (or the recent history), growth, hours per full-time agent, current agents, attrition and hiring classes. Read-only here; getCapacityPlanProjection gives its projection, and it is changed on the Capacity Plans page"])
            .WithPermission(WorkforcePermissions.ManageForecasts)
            .UseCatalogManager<CapacityPlanManager>()
            .ListUsing(ContactCenterRecordKinds.ListByName<CapacityPlan, CapacityPlanManager>(plan => plan.Name))
            .WithProjection(plan => new
            {
                id = plan.ItemId,
                name = plan.Name,
                planningGroupId = plan.PlanningGroupId,
                startDate = plan.StartDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                weeks = plan.Weeks,
                weeklyWorkloadHours = plan.WeeklyWorkloadHours,
                growthPercentPerYear = plan.GrowthPercentPerYear,
                hoursPerFte = plan.HoursPerFte,
                currentFte = plan.CurrentFte,
                attritionPercentPerMonth = plan.AttritionPercentPerMonth,
                hiringClasses = plan.HiringClasses.Select(hiring => new
                {
                    startDate = hiring.StartDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    size = hiring.Size,
                    trainingWeeks = hiring.TrainingWeeks,
                    rampWeeks = hiring.RampWeeks,
                }).ToArray(),
            })
            .ReadOnly());
    }
}

/// <summary>
/// Lists the records of a contact center workforce kind a page at a time, by name.
/// </summary>
internal static class ContactCenterRecordKinds
{
    /// <summary>
    /// Lists the records of the manager whose names contain the query's text, ordered by name.
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <typeparam name="TManager">The manager the admin page uses.</typeparam>
    /// <param name="name">Gets a record's name.</param>
    /// <returns>The list function.</returns>
    public static Func<IServiceProvider, AIRecordQuery, CancellationToken, ValueTask<PageResult<T>>> ListByName<T, TManager>(Func<T, string> name)
        where T : CatalogItem
        where TManager : ICatalogManager<T>
    {
        return async (services, query, cancellationToken) =>
        {
            var records = (await services.GetRequiredService<TManager>().GetAllAsync(cancellationToken))
                .Where(record => string.IsNullOrWhiteSpace(query.Text) || (name(record) ?? string.Empty).Contains(query.Text.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(record => name(record), StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Max(1, query.PageSize);

            return new PageResult<T>
            {
                Count = records.Count,
                Entries = [.. records.Skip((page - 1) * pageSize).Take(pageSize)],
            };
        };
    }
}
