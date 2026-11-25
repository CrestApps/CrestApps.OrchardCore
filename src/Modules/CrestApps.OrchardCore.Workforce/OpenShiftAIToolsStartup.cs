using CrestApps.Core.AI;
using CrestApps.OrchardCore.Workforce.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Adds the open shift and shift trade AI tools for managers: the open shift list, posting, giving a shift to a worker or
/// declining a claim, canceling, and the shift trade list, offers and decisions. They work only for a signed-in person
/// with the permission of the matching admin page, in the teams that person manages.
/// </summary>
[Feature(WorkforceConstants.Features.ShiftPlanning)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class OpenShiftAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenShiftAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OpenShiftAIToolsStartup(IStringLocalizer<OpenShiftAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListOpenShiftsTool>(ListOpenShiftsTool.ToolName)
            .WithTitle(S["List open shifts"])
            .WithDescription(S["Lists open shifts filtered by team, status and date, each with its claims and, when asked, the workers who are eligible to take it."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<PostOpenShiftTool>(PostOpenShiftTool.ToolName)
            .WithTitle(S["Post open shift"])
            .WithDescription(S["Posts a shift nobody has, for a team, day and time, optionally from a shift template that brings its breaks and the skills needed. The team's settings decide how it is claimed (first come, approval or seniority) and the deadline. Eligible workers are notified."])
            .WithCategory(S["Workforce"])
            .Selectable()
            .AsWrite()
            .RequireApproval();

        services.AddCoreAITool<DecideOpenShiftClaimTool>(DecideOpenShiftClaimTool.ToolName)
            .WithTitle(S["Decide open shift claim"])
            .WithDescription(S["Gives an open shift to a worker (one who claimed it or any eligible worker), or declines a worker's claim with a note. The worker is notified."])
            .WithCategory(S["Workforce"])
            .Selectable()
            .AsExternal();

        services.AddCoreAITool<CancelOpenShiftTool>(CancelOpenShiftTool.ToolName)
            .WithTitle(S["Cancel open shift"])
            .WithDescription(S["Cancels an open shift so it can no longer be claimed."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<ListShiftTradesTool>(ListShiftTradesTool.ToolName)
            .WithTitle(S["List shift trades"])
            .WithDescription(S["Lists shift trades and giveaways filtered by team, worker and status, with the shifts involved and any rule problems."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<CreateShiftTradeTool>(CreateShiftTradeTool.ToolName)
            .WithTitle(S["Create shift trade"])
            .WithDescription(S["Offers one worker's shift to another worker, as a giveaway or a swap for one of theirs, on the worker's behalf. The other worker is asked to accept."])
            .WithCategory(S["Workforce"])
            .Selectable()
            .AsExternal();

        services.AddCoreAITool<DecideShiftTradeTool>(DecideShiftTradeTool.ToolName)
            .WithTitle(S["Decide shift trade"])
            .WithDescription(S["Approves, declines (with a note) or cancels a shift trade, or accepts or rejects it for the other worker. Approving swaps the shifts on the published schedule and notifies both workers."])
            .WithCategory(S["Workforce"])
            .Selectable()
            .AsExternal();
    }
}

/// <summary>
/// Adds the worker's own open shift AI tools: the open shifts of the worker's teams, and claiming one or withdrawing a
/// claim. They act only for the worker linked to the signed-in person's account and need the My Work permission, so they
/// need the My Work Portal feature as well.
/// </summary>
[Feature(WorkforceConstants.Features.ShiftPlanning)]
[RequireFeatures(AIFeatureIds.Area, WorkforceConstants.Features.Portal)]
public sealed class MyOpenShiftsAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyOpenShiftsAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MyOpenShiftsAIToolsStartup(IStringLocalizer<MyOpenShiftsAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListMyOpenShiftsTool>(ListMyOpenShiftsTool.ToolName)
            .WithTitle(S["My open shifts"])
            .WithDescription(S["Lists the open shifts of the user's own teams from today, whether the user has claimed each and, when not, why they couldn't take it."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<ClaimOpenShiftTool>(ClaimOpenShiftTool.ToolName)
            .WithTitle(S["Claim open shift"])
            .WithDescription(S["Claims an open shift for the user, or withdraws their claim. With first come, first served the shift is theirs at once; otherwise a manager decides."])
            .WithCategory(S["Workforce"])
            .Selectable();
    }
}
