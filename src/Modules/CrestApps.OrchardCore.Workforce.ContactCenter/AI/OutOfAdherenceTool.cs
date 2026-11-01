using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.AI;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.AI;

/// <summary>
/// Lists the agents out of adherence now in the teams the user manages: what they are scheduled to do, what they are
/// doing, and for how long.
/// </summary>
public sealed class OutOfAdherenceTool : WorkforceAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "workforceOutOfAdherence";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "teamName": { "type": "string", "description": "Optional. One team the user manages; its sub-teams are included." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Lists the contact center agents out of adherence right now in the teams the user manages: each agent's team, the scheduled activity, the agent's state and reason, how many minutes they have been out, and whether an alert was raised.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, WorkforcePermissions.ViewAdherence))
        {
            return "You aren't allowed to see adherence.";
        }

        string teamId = null;

        if (Read(arguments, "teamName") is { } teamName)
        {
            var team = (await services.GetRequiredService<ITeamManager>().GetAllAsync(cancellationToken))
                .FirstOrDefault(item => WorkforceNames.Normalize(item.Name) == WorkforceNames.Normalize(teamName));

            if (team is null)
            {
                return $"No team is called '{teamName}'.";
            }

            teamId = team.ItemId;
        }

        var rows = await services.GetRequiredService<AdherenceService>().BoardAsync(user, teamId, services.GetRequiredService<IWorkforceScopeService>(), cancellationToken);
        var now = services.GetRequiredService<IClock>().UtcNow;
        var scheduled = rows.Where(row => row.Scheduled is not null).ToList();

        return JsonSerializer.Serialize(new
        {
            agentsScheduledNow = scheduled.Count,
            inAdherence = scheduled.Count(row => row.InAdherence),
            outOfAdherence = scheduled.Where(row => !row.InAdherence).Select(row => new
            {
                name = row.WorkerName,
                team = row.TeamName,
                scheduled = row.Scheduled.Kind.ToString(),
                state = row.Status.ToString(),
                reason = row.Reason,
                minutesOut = row.OutSinceUtc.HasValue ? (int)Math.Max(0, (now - row.OutSinceUtc.Value).TotalMinutes) : (int?)null,
                alert = row.Alert,
            }),
        }, JsonOptions);
    }
}
