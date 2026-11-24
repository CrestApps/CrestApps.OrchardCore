using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Descriptor.Models;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Lists agent profiles with what the Agent entitlements page manages: allowed queues and campaigns, skills,
/// memberships and the concurrency limit, beside what the agent is signed in to now.
/// </summary>
internal sealed class ListContactCenterAgentsTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listContactCenterAgents";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "user": {
          "type": "string",
          "description": "Only agents whose user name or display name contains this text."
        },
        "queueId": {
          "type": "string",
          "description": "Only agents allowed in, a member of, or signed in to this queue, by ID (see listRecords with the kind contactCenterQueue)."
        },
        "skill": {
          "type": "string",
          "description": "Only agents with this skill, by name."
        },
        "page": {
          "type": "integer",
          "description": "The page to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "How many agents per page, at most 50. Default 20."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists Contact Center agent profiles with user, presence, signed-in queues and campaigns, allowed queues and campaigns, skills with proficiency, queue memberships and concurrency limit. Filter by user, queue or skill. Says whether allow-lists are enforced (the Agent Entitlements feature).";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.ManageAgents;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var reader = new ContactCenterToolArguments(arguments);
        var (page, pageSize) = ReadPaging(reader);
        var search = reader.GetString("user");
        var queueId = reader.GetString("queueId");
        var skill = reader.GetString("skill");
        var skillTag = skill is not null && SkillTag.TryCreate(skill, out var tag) ? tag : (SkillTag?)null;

        var agents = (await services.GetRequiredService<IAgentProfileManager>().GetAllAsync(cancellationToken))
            .Where(agent => agent is not null)
            .Where(agent => search is null ||
                agent.UserName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
                agent.DisplayName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
            .Where(agent => queueId is null || ServesQueue(agent, queueId))
            .Where(agent => skillTag is null || HasSkill(agent, skillTag.Value))
            .OrderBy(agent => agent.UserName ?? agent.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var describer = await ContactCenterAgentDescriber.CreateAsync(services, cancellationToken);
        var (items, total, truncated) = ContactCenterSetupLookups.Page(agents, page, pageSize);
        var enforced = IsEntitlementsEnabled(services);

        return Json(new
        {
            entitlementsEnforced = enforced,
            note = enforced
                ? null
                : "The Agent Entitlements feature is off, so any agent may sign in to any queue or campaign; the allowed lists are not enforced.",
            items = items.Select(describer.Describe).ToList(),
            total,
            page,
            pageSize,
            truncated,
        });
    }

    /// <summary>
    /// Gets whether the Agent Entitlements feature, which enforces the allow-lists, is enabled.
    /// </summary>
    internal static bool IsEntitlementsEnabled(IServiceProvider services)
        => services.GetService<ShellDescriptor>()?.Features.Any(feature => feature.Id == ContactCenterConstants.Feature.AgentEntitlements) == true;

    private static bool ServesQueue(AgentProfile agent, string queueId)
        => (agent.AllowedQueueIds ?? []).Contains(queueId, StringComparer.OrdinalIgnoreCase) ||
            (agent.QueueIds ?? []).Contains(queueId, StringComparer.OrdinalIgnoreCase) ||
            (agent.QueueMemberships ?? []).Any(membership => string.Equals(membership?.QueueId, queueId, StringComparison.OrdinalIgnoreCase));

    private static bool HasSkill(AgentProfile agent, SkillTag skill)
        => (agent.Skills ?? []).Concat((agent.SkillProficiencies ?? []).Where(proficiency => proficiency is not null).Select(proficiency => proficiency.SkillId))
            .Any(name => SkillTag.TryCreate(name, out var candidate) && candidate.Equals(skill));
}
