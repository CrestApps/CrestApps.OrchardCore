using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Teams.AI;

/// <summary>
/// Lists the team hierarchy as Content › Teams does (<see cref="ITeamManager.ListTreeAsync"/>): each team followed by
/// its sub-teams, filtered by text in the name. It needs <see cref="TeamsPermissions.ManageTeams"/>, as the page does.
/// </summary>
internal sealed class ListTeamsTool : TeamsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listTeams";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "Text the team's name contains, ignoring case. Leave it out to list every team."
        },
        "page": {
          "type": "integer",
          "description": "The page, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "How many teams per page, at most 50 (20 by default)."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the team hierarchy (organization, regions, sites, departments, teams, crews) with id, kind, parent, linked role, time zone and whether each is enabled; filter by name. Use it to get a team id before assigning people or editing a team.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var tree = await services.GetRequiredService<ITeamManager>().ListTreeAsync(cancellationToken);
        var names = tree.Where(item => item.Team.ItemId is not null).ToDictionary(item => item.Team.ItemId, item => item.Team.Name, StringComparer.Ordinal);
        var search = ReadString(arguments, "name");
        var matches = tree
            .Where(item => search is null || (item.Team.Name ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var page = Math.Max(1, ReadInt(arguments, "page") ?? 1);
        var pageSize = Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize);
        var items = matches.Skip((page - 1) * pageSize).Take(pageSize).Select(item => Describe(item.Team, names, item.Depth)).ToList();

        return Json(new
        {
            items,
            total = matches.Count,
            page,
            pageSize,
            truncated = (page - 1) * pageSize + items.Count < matches.Count,
        });
    }
}
