using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using YesSql;
using static CrestApps.OrchardCore.Workforce.ContactCenter.AI.ContactCenterWorkforceTools;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.AI;

/// <summary>
/// Creates a worker for every contact center agent that has none, as the Contact Center Agents page's Create workers
/// button does.
/// </summary>
internal sealed class CreateWorkersForAgentsTool : ContactCenterWorkforceToolBase
{
    public const string ToolName = "createWorkersForAgents";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "teamName": { "type": "string", "description": "Optional. The team (name or ID) the new workers join as their primary team." }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Creates a worker profile for every contact center agent that has none, optionally in a team, and returns how many were created. Use it after turning Workforce on for a contact center that already has agents.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageWorkers;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        string teamId = null;
        string teamName = null;

        if (Read(arguments, "teamName") is { } requested)
        {
            var team = await FindTeamAsync(services, requested, cancellationToken);

            if (team is null)
            {
                return $"No team is called '{requested}'. Nothing was created.";
            }

            teamId = team.ItemId;
            teamName = team.Name;
        }

        var agents = services.GetRequiredService<AgentWorkerService>();
        var missing = (await agents.ListAsync(cancellationToken)).Count(entry => entry.Worker is null);

        if (missing == 0)
        {
            return ToJson(new
            {
                created = 0,
                message = "Every contact center agent with a user already has a worker; nothing was created.",
            });
        }

        var created = await agents.CreateMissingAsync(teamId, cancellationToken);

        await services.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);

        return ToJson(new
        {
            created,
            agentsWithoutWorker = missing,
            team = teamName,
            message = created == missing
                ? "Every agent without a worker now has one."
                : "Some agents could not get a worker; check them on the Contact Center Agents page.",
        });
    }
}
