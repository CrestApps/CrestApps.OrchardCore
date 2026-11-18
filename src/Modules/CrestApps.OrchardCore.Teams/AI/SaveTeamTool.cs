using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Teams.AI;

/// <summary>
/// Creates or changes a team through the team manager, whose catalog handlers hold the rules the team editor reports: a
/// name is required, the parent must exist, a team can't sit under itself or one of its sub-teams, and the time zone must
/// be a known IANA id. Only the core fields given change; what other modules keep on a team (their editor cards, such as
/// Workforce or Contact Center settings, or its address) is kept as it is. It needs
/// <see cref="TeamsPermissions.ManageTeams"/>.
/// </summary>
internal sealed class SaveTeamTool : TeamsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveTeam";

    private static readonly string[] _fieldNames = ["name", "kind", "parentId", "description", "linkedRoleName", "timeZoneId", "enabled"];

    private static readonly TeamKind[] _kinds =
    [
        TeamKind.Organization,
        TeamKind.Region,
        TeamKind.Site,
        TeamKind.Department,
        TeamKind.Team,
        TeamKind.Crew,
    ];

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "teamId": {
          "type": "string",
          "description": "The id of the team to change, from listTeams. Leave it out to create a team."
        },
        "name": {
          "type": "string",
          "description": "The team's name. Required for a new team."
        },
        "kind": {
          "type": "string",
          "enum": ["Organization", "Region", "Site", "Department", "Team", "Crew"],
          "description": "What level of the organization the team is. A new team is a Team when left out."
        },
        "parentId": {
          "type": "string",
          "description": "The id of the team it belongs to, from listTeams. An empty text puts it at the top level. A team can't be placed under itself or one of its sub-teams."
        },
        "description": {
          "type": "string",
          "description": "What the team does. An empty text removes it."
        },
        "linkedRoleName": {
          "type": "string",
          "description": "The name of a role whose members are treated as part of the team, such as for reading its calendar. An empty text removes it."
        },
        "timeZoneId": {
          "type": "string",
          "description": "The team's IANA time zone, such as America/Phoenix, which sets its day boundaries. An empty text uses the site's time zone."
        },
        "enabled": {
          "type": "boolean",
          "description": "Whether the team is in use. A new team is enabled when left out."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates a team: name, kind, parent, description, linked role, time zone and enabled. A team cannot be moved under itself or one of its sub-teams. Only the fields given change; settings other modules keep on the team are kept.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<ITeamManager>();
        var teamId = ReadString(arguments, "teamId");
        Team team;

        if (teamId is null)
        {
            team = await manager.NewAsync(cancellationToken: cancellationToken);
        }
        else
        {
            team = await manager.FindByIdAsync(teamId, cancellationToken);

            if (team is null)
            {
                return $"Nothing was saved: there is no team with the id '{teamId}'. Use listTeams to find it, or leave teamId out to create one.";
            }

            if (!_fieldNames.Any(name => Has(arguments, name)))
            {
                return "Nothing was changed: give at least one field to change.";
            }
        }

        if (Has(arguments, "kind"))
        {
            if (!Enum.TryParse<TeamKind>(ReadString(arguments, "kind"), ignoreCase: true, out var kind) || !_kinds.Contains(kind))
            {
                return "Nothing was saved: kind must be Organization, Region, Site, Department, Team or Crew.";
            }

            team.Kind = kind;
        }

        if (Has(arguments, "name"))
        {
            team.Name = ReadString(arguments, "name");
        }

        if (Has(arguments, "parentId"))
        {
            team.ParentId = ReadString(arguments, "parentId");
        }

        if (Has(arguments, "description"))
        {
            team.Description = ReadString(arguments, "description");
        }

        if (Has(arguments, "linkedRoleName"))
        {
            team.LinkedRoleName = ReadString(arguments, "linkedRoleName");
        }

        if (Has(arguments, "timeZoneId"))
        {
            team.TimeZoneId = ReadString(arguments, "timeZoneId");
        }

        if (Has(arguments, "enabled"))
        {
            team.Enabled = ReadBool(arguments, "enabled") ?? team.Enabled;
        }

        var validation = await manager.ValidateAsync(team, cancellationToken);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Where(message => !string.IsNullOrEmpty(message)))}";
        }

        if (teamId is null)
        {
            await manager.CreateAsync(team, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(team, cancellationToken: cancellationToken);
        }

        var tree = await manager.ListTreeAsync(cancellationToken);
        var names = tree.Where(item => item.Team.ItemId is not null).ToDictionary(item => item.Team.ItemId, item => item.Team.Name, StringComparer.Ordinal);

        if (team.ParentId is not null && !names.ContainsKey(team.ParentId) && await manager.FindByIdAsync(team.ParentId, cancellationToken) is { } parent)
        {
            names[parent.ItemId] = parent.Name;
        }

        return Json(new
        {
            team = Describe(team, names),
            created = teamId is null,
            message = teamId is null ? "The team has been created." : "The team has been saved.",
        });
    }
}
