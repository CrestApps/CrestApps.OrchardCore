using CrestApps.Core.AI;
using CrestApps.OrchardCore.Teams.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Teams;

/// <summary>
/// Registers the team hierarchy AI tools whenever the Teams and AI features are on. Each needs <c>ManageTeams</c>, as
/// the Teams pages do; deleting a team asks for approval.
/// </summary>
[RequireFeatures(AIFeatureIds.Area)]
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

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListTeamsTool>(ListTeamsTool.TheName)
            .WithTitle(S["List Teams"])
            .WithDescription(S["Lists the team hierarchy (organization, regions, sites, departments, teams, crews) with id, kind, parent, linked role, time zone and whether each is enabled; filter by name. Use it to get a team id before assigning people or editing a team."])
            .WithCategory(S["Teams"])
            .Selectable();

        services.AddCoreAITool<SaveTeamTool>(SaveTeamTool.TheName)
            .WithTitle(S["Save Team"])
            .WithDescription(S["Creates or updates a team: name, kind, parent, description, linked role, time zone and enabled. A team cannot be moved under itself or one of its sub-teams."])
            .WithCategory(S["Teams"])
            .Selectable();

        services.AddCoreAITool<DeleteTeamTool>(DeleteTeamTool.TheName)
            .WithTitle(S["Delete Team"])
            .WithDescription(S["Deletes a team. It is refused, with the reasons, while the team still has sub-teams or another module still depends on it."])
            .WithCategory(S["Teams"])
            .RequireApproval()
            .Selectable();
    }
}
