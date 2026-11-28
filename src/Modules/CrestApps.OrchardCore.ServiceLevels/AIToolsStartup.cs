using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.AI;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ServiceLevels;

/// <summary>
/// The service-level AI tools, which administrators can pick on AI profiles, and the service level policies as a
/// read-only record kind of the generic record tools, whenever the AI feature is on. The tools read the timers of every
/// kind of work with service levels (tickets, work orders...), always within what the signed-in user may see.
/// </summary>
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    /// <summary>
    /// The name of the service level policy record kind.
    /// </summary>
    public const string PolicyKind = "serviceLevelPolicy";

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecordDirectory();
        services.TryAddScoped<ServiceLevelDueTimers>();

        services.AddCoreAITool<ListServiceLevelRisksTool>(ListServiceLevelRisksTool.TheName)
            .WithTitle(S["List service level risks"])
            .WithDescription(S["Lists the tickets, work orders and other work whose service-level targets are past due, at risk or due soon, most overdue first."])
            .WithCategory(S["Service Levels"])
            .Selectable();

        services.AddCoreAITool<GetServiceLevelTimersTool>(GetServiceLevelTimersTool.TheName)
            .WithTitle(S["Get service level timers"])
            .WithDescription(S["Gets the service-level timers of one record, such as a ticket: each target, when it is due and how it stands."])
            .WithCategory(S["Service Levels"])
            .Selectable();

        // The policy editor's targets grid is a list of objects and a new policy's scope comes from the page's address,
        // which the editor replay cannot post, so the policies are listed and read only.
        services.AddAIRecordKind<ServiceLevelPolicy>(PolicyKind, kind => kind
            .WithTitle(S["Service level policy"])
            .WithDescription(S["a service level policy: the minutes allowed for each metric (FirstResponse, NextResponse, Resolution) at each priority, counted on a business-hours schedule (none means around the clock). A target's priority is the key its scope uses (for tickets, the priority id from listTicketOptions); an empty priority means every other priority. Read-only: changed on Service Levels"])
            .WithPermission(ServiceLevelsPermissions.ManageServiceLevelPolicies)
            .UseCatalogManager<IServiceLevelPolicyManager>()
            .WithProjection(DescribePolicy)
            .ReadOnly());
    }

    /// <summary>
    /// The allow-listed fields of a policy.
    /// </summary>
    /// <param name="policy">The policy.</param>
    /// <returns>The fields.</returns>
    internal static object DescribePolicy(ServiceLevelPolicy policy)
        => new
        {
            id = policy.ItemId,
            name = policy.Name,
            description = policy.Description,
            scope = policy.Scope,
            hoursScheduleId = policy.HoursScheduleId,
            warningPercent = policy.WarningPercent,
            targets = (policy.Targets ?? [])
                .Where(target => target is not null && target.Minutes > 0)
                .Select(target => new
                {
                    metric = target.Metric,
                    priority = string.IsNullOrEmpty(target.Priority) ? null : target.Priority,
                    minutes = target.Minutes,
                })
                .ToArray(),
        };
}
