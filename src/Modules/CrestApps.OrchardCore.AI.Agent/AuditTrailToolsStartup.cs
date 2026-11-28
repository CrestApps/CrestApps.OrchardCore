using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.AuditTrail;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list audit trail events and read one event, when the Audit Trail feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.AuditTrail)]
public sealed class AuditTrailToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditTrailToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AuditTrailToolsStartup(IStringLocalizer<AuditTrailToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListAuditTrailEventsTool>(ListAuditTrailEventsTool.TheName)
            .WithTitle(S["List Audit Trail Events"])
            .WithDescription(S["Lists audit trail events, newest first, with optional filters for category, event name, user, date range and correlation id, and paging. Use it to answer \"who did what and when\"."])
            .WithCategory(S["Audit Trail"])
            .Selectable();

        services.AddCoreAITool<GetAuditTrailEventTool>(GetAuditTrailEventTool.TheName)
            .WithTitle(S["Get Audit Trail Event"])
            .WithDescription(S["Gets one audit trail event by id with its details, such as the user snapshot or the content item version it recorded."])
            .WithCategory(S["Audit Trail"])
            .Selectable();
    }
}
