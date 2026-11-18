using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Teams.AI;

/// <summary>
/// Deletes a team as the Teams page's Delete action does: refused, with the reasons, while
/// <see cref="ITeamManager.GetDeletionBlockersAsync"/> reports the team still has sub-teams or a module that uses it (such
/// as Workforce while it has members) holds it. It needs <see cref="TeamsPermissions.ManageTeams"/> and asks for approval.
/// </summary>
internal sealed class DeleteTeamTool : TeamsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteTeam";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "teamId": {
          "type": "string",
          "description": "The id of the team to delete, from listTeams."
        }
      },
      "required": ["teamId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a team. It is refused, with the reasons, while the team still has sub-teams or another module still depends on it.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<ITeamManager>();
        var teamId = ReadString(arguments, "teamId");
        var team = teamId is null ? null : await manager.FindByIdAsync(teamId, cancellationToken);

        if (team is null)
        {
            return $"Nothing was deleted: there is no team with the id '{teamId}'. Use listTeams to find it.";
        }

        var blockers = await manager.GetDeletionBlockersAsync(team, cancellationToken);

        if (blockers.Count > 0)
        {
            return Json(new
            {
                deleted = false,
                id = team.ItemId,
                name = team.Name,
                reasons = blockers,
                message = $"The team can't be deleted. {string.Join(" ", blockers)}",
            });
        }

        await manager.DeleteAsync(team, cancellationToken);

        return Json(new
        {
            deleted = true,
            id = team.ItemId,
            name = team.Name,
            message = "The team has been deleted.",
        });
    }
}
