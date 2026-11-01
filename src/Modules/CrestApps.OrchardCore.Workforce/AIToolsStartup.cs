using CrestApps.Core.AI;
using CrestApps.OrchardCore.Workforce.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Adds the Workforce AI tools: who is working, a person's own schedule, and finding cover.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI")]
public sealed class AIToolsStartup : StartupBase
{
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
        services.AddCoreAITool<WhoIsWorkingTool>(WhoIsWorkingTool.ToolName)
            .WithTitle(S["Who is working"])
            .WithDescription(S["Lists who is on shift at a time in the teams the user manages."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<MyScheduleTool>(MyScheduleTool.ToolName)
            .WithTitle(S["My schedule"])
            .WithDescription(S["Lists the user's own shifts."])
            .WithCategory(S["Workforce"])
            .Selectable();
    }
}

/// <summary>
/// Adds the shift planning AI tools: finding cover and explaining labor rule problems.
/// </summary>
[Feature(WorkforceConstants.Features.ShiftPlanning)]
[RequireFeatures("CrestApps.OrchardCore.AI")]
public sealed class ShiftPlanningAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShiftPlanningAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ShiftPlanningAIToolsStartup(IStringLocalizer<ShiftPlanningAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<FindCoverTool>(FindCoverTool.ToolName)
            .WithTitle(S["Find cover"])
            .WithDescription(S["Finds colleagues who can take a worker's shift."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<ExplainViolationsTool>(ExplainViolationsTool.ToolName)
            .WithTitle(S["Explain schedule problems"])
            .WithDescription(S["Lists the labor rule problems of a team's draft schedule."])
            .WithCategory(S["Workforce"])
            .Selectable();
    }
}

/// <summary>
/// Adds the time-off AI tools: balances and asking for time off.
/// </summary>
[Feature(WorkforceConstants.Features.TimeOff)]
[RequireFeatures("CrestApps.OrchardCore.AI")]
public sealed class TimeOffAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeOffAIToolsStartup(IStringLocalizer<TimeOffAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<TimeOffBalanceTool>(TimeOffBalanceTool.ToolName)
            .WithTitle(S["My time-off balance"])
            .WithDescription(S["Gives the user's own time-off balances."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<RequestTimeOffTool>(RequestTimeOffTool.ToolName)
            .WithTitle(S["Request time off"])
            .WithDescription(S["Asks for time off for the user."])
            .WithCategory(S["Workforce"])
            .Selectable();
    }
}
