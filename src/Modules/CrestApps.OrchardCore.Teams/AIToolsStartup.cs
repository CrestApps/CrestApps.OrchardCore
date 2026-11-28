using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Teams.AI;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Teams;

/// <summary>
/// Registers the team AI tools whenever the Teams and AI features are on: the teams as the <c>team</c> kind of the
/// generic record tools (<c>listRecords</c>, <c>getRecord</c>, <c>saveRecord</c> and <c>deleteRecord</c>), whose writes
/// replay the team editor of Content › Teams with the team manager's rules, and <c>listTeamMembers</c>. Both need
/// <c>ManageTeams</c>, as the Teams pages do.
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
        services.AddCoreAITool<ListTeamMembersTool>(ListTeamMembersTool.TheName)
            .WithTitle(S["List Team Members"])
            .WithDescription(S["Lists who is in a team on a day (today by default), with the members of its sub-teams unless told otherwise: each person's name, user id and staff member id, role, the team or sub-team they are in and the dates of the membership. Use it for \"who is on the Phoenix crew\"."])
            .WithCategory(S["Teams"])
            .Selectable();

        // The team editor is built by display drivers (Teams' own fields, and Workforce's settings when it is on), so the
        // writes replay it; the team manager's rules (a name, a known parent, no loop, a known time zone) are checked after.
        services.AddAIRecordKind<Team>(TeamRecordKind.Name, kind => kind
            .WithTitle(S["Team"])
            .WithDescription(S["a unit of the team hierarchy that staff, schedules, queues and calendars point to, with its parent team, linked role, time zone, postal address and enabled flag. Kind is Organization, Region, Site, Department, Team or Crew; ParentId is another team's id (empty for a top team); TimeZoneId is an IANA id. A team with sub-teams, or that another module uses, is not deleted"])
            .WithPermission(TeamsPermissions.ManageTeams)
            .UseCatalogManager<ITeamManager>()
            .ListUsing(TeamRecordKind.ListAsync)
            .WithProjection(TeamRecordKind.Project)
            .WithInUseCheck(TeamRecordKind.GetUseAsync)
            .UseEditorReplay());
    }
}
