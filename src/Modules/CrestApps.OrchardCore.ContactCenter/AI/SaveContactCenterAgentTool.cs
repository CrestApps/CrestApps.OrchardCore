using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Creates the agent profile of an Orchard user, or changes what an agent may do, the way the Agent entitlements page and
/// the agent entitlement recipe step do: a new profile is built by <see cref="AgentProfileFactory"/>, and a change goes
/// through <see cref="IAgentPresenceManager.ApplyManagedConfigurationAsync"/>, which also signs the agent out of queues
/// and campaigns they are no longer allowed in.
/// </summary>
internal sealed class SaveContactCenterAgentTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveContactCenterAgent";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "agentId": { "type": "string", "description": "The ID of the agent profile to change (see listContactCenterAgents)." },
        "userName": { "type": "string", "description": "The Orchard user name of the agent. Creates the user's agent profile when it has none." },
        "allowedQueueIds": { "type": "array", "items": { "type": "string" }, "description": "The queues the agent may sign in to. Replaces the whole list." },
        "allowedCampaignIds": { "type": "array", "items": { "type": "string" }, "description": "The dialer campaigns the agent may sign in to. Replaces the whole list." },
        "skills": {
          "type": "array",
          "description": "The agent's skills with proficiency. Replaces the whole list.",
          "items": {
            "type": "object",
            "properties": {
              "skillId": { "type": "string", "description": "The skill name." },
              "proficiency": { "type": "integer", "minimum": 1, "maximum": 5, "description": "The proficiency, 1 to 5. Default 3." }
            },
            "required": ["skillId"],
            "additionalProperties": false
          }
        },
        "queueMemberships": {
          "type": "array",
          "description": "Routing preference per allowed queue. Replaces the whole list.",
          "items": {
            "type": "object",
            "properties": {
              "queueId": { "type": "string", "description": "An allowed queue." },
              "priority": { "type": "integer", "minimum": 0, "description": "The agent's priority for the queue; 0 is the highest." },
              "delaySeconds": { "type": "integer", "minimum": 0, "description": "Seconds an item waits before it is offered to this agent." }
            },
            "required": ["queueId"],
            "additionalProperties": false
          }
        },
        "maxConcurrentInteractions": { "type": "integer", "minimum": 1, "description": "The most interactions the agent handles at once." }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates the agent profile of an Orchard user or updates which queues and campaigns the agent may sign in to, their skills and proficiency, queue memberships and maximum concurrent interactions. Lists given replace the whole list; fields left out are kept. It changes who can receive which customers' calls.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.ManageAgents;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var reader = new ContactCenterToolArguments(arguments);
        var agentManager = services.GetRequiredService<IAgentProfileManager>();
        var agentId = reader.GetString("agentId");
        var userName = reader.GetString("userName");
        AgentProfile agent = null;
        IUser orchardUser = null;

        if (agentId is not null)
        {
            agent = await agentManager.FindByIdAsync(agentId, cancellationToken);

            if (agent is null)
            {
                return $"Nothing was saved: no agent profile has the ID '{agentId}'. Call listContactCenterAgents to find it.";
            }
        }
        else if (userName is not null)
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            orchardUser = await userManager.FindByNameAsync(userName);

            if (orchardUser is null)
            {
                return $"Nothing was saved: no user is named '{userName}'.";
            }

            agent = await agentManager.FindByUserIdAsync(await userManager.GetUserIdAsync(orchardUser), cancellationToken);
        }
        else
        {
            return "Nothing was saved: give the agentId of an agent profile, or the userName of the user whose profile to create or change.";
        }

        var allowedQueueIds = reader.GetStringList("allowedQueueIds");
        var allowedCampaignIds = reader.GetStringList("allowedCampaignIds");
        var skills = ReadSkills(reader);
        var memberships = ReadMemberships(reader);
        reader.Int("maxConcurrentInteractions", nameof(AgentProfile.MaxConcurrentInteractions), minimum: 1);

        if (reader.Errors.Count > 0)
        {
            return "Nothing was saved: " + string.Join(" ", reader.Errors);
        }

        // The editor lists only existing queues and campaigns; a typed ID that names nothing is refused, not dropped.
        var optionsProvider = services.GetRequiredService<ContactCenterAdminFormOptionsProvider>();
        var problems = new List<string>();

        if (allowedQueueIds is not null)
        {
            var existing = await optionsProvider.FilterExistingQueueIdsAsync(allowedQueueIds);
            problems.AddRange(allowedQueueIds.Where(id => !existing.Contains(id, StringComparer.OrdinalIgnoreCase)).Select(id => $"No queue has the ID '{id}'."));
            allowedQueueIds = [.. existing];
        }

        if (allowedCampaignIds is not null)
        {
            var existing = await optionsProvider.FilterExistingCampaignIdsAsync(allowedCampaignIds);
            problems.AddRange(allowedCampaignIds.Where(id => !existing.Contains(id, StringComparer.OrdinalIgnoreCase)).Select(id => $"No campaign has the ID '{id}'."));
            allowedCampaignIds = [.. existing];
        }

        // A membership is a preference for a queue the agent may sign in to; the editor offers one row per allowed queue.
        var allowed = new HashSet<string>(allowedQueueIds ?? agent?.AllowedQueueIds ?? [], StringComparer.OrdinalIgnoreCase);

        if (memberships is not null)
        {
            problems.AddRange(memberships.Where(membership => !allowed.Contains(membership.QueueId)).Select(membership => $"Queue '{membership.QueueId}' is not among the agent's allowed queues, so it cannot have a membership."));
        }
        else if (allowedQueueIds is not null && agent is not null)
        {
            memberships = (agent.QueueMemberships ?? []).Where(membership => membership is not null && allowed.Contains(membership.QueueId ?? string.Empty)).ToList();
        }

        if (problems.Count > 0)
        {
            return "Nothing was saved: " + string.Join(" ", problems);
        }

        var maxConcurrent = reader.GetInt("maxConcurrentInteractions");

        if (agent is null)
        {
            return await CreateAsync(services, agentManager, orchardUser, allowedQueueIds, allowedCampaignIds, skills, memberships, maxConcurrent, cancellationToken);
        }

        var configuration = new AgentManagedConfiguration
        {
            DisplayName = agent.DisplayName,
            MaxConcurrentInteractions = maxConcurrent ?? agent.MaxConcurrentInteractions,
            AllowedQueueIds = allowedQueueIds ?? agent.AllowedQueueIds,
            AllowedCampaignIds = allowedCampaignIds ?? agent.AllowedCampaignIds,

            // Skills given are the whole set, as on the entitlements page; otherwise the agent's skills are passed through.
            Skills = skills is null ? agent.Skills : null,
            SkillProficiencies = skills ?? agent.SkillProficiencies,
            QueueMemberships = memberships ?? agent.QueueMemberships,
        };

        AgentProfile updated;

        try
        {
            updated = await services.GetRequiredService<IAgentPresenceManager>().ApplyManagedConfigurationAsync(agent.ItemId, configuration, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return "Nothing was saved: the agent's profile is being changed by something else right now. Try again in a moment.";
        }

        if (updated is null)
        {
            return $"Nothing was saved: the agent profile '{agent.ItemId}' no longer exists.";
        }

        var describer = await ContactCenterAgentDescriber.CreateAsync(services, cancellationToken);

        return Json(new
        {
            saved = "updated",
            entitlementsEnforced = ListContactCenterAgentsTool.IsEntitlementsEnabled(services),
            agent = describer.Describe(updated),
        });
    }

    private static async Task<object> CreateAsync(
        IServiceProvider services,
        IAgentProfileManager agentManager,
        IUser orchardUser,
        List<string> allowedQueueIds,
        List<string> allowedCampaignIds,
        List<AgentSkill> skills,
        List<AgentQueueMembership> memberships,
        int? maxConcurrent,
        CancellationToken cancellationToken)
    {
        var userManager = services.GetRequiredService<UserManager<IUser>>();
        var userName = await userManager.GetUserNameAsync(orchardUser);
        var agent = await AgentProfileFactory.NewAsync(
            agentManager,
            await userManager.GetUserIdAsync(orchardUser),
            userName,
            new AgentManagedConfiguration
            {
                DisplayName = userName,
                MaxConcurrentInteractions = maxConcurrent ?? 1,
                AllowedQueueIds = allowedQueueIds,
                AllowedCampaignIds = allowedCampaignIds,
                SkillProficiencies = skills ?? [],
                QueueMemberships = memberships,
            },
            services.GetRequiredService<IClock>().UtcNow);

        var validation = await agentManager.ValidateAsync(agent, cancellationToken);

        if (!validation.Succeeded)
        {
            return CatalogWriteResult<AgentProfile>.Invalid(validation).Message;
        }

        await agentManager.CreateAsync(agent, cancellationToken);

        var describer = await ContactCenterAgentDescriber.CreateAsync(services, cancellationToken);

        return Json(new
        {
            saved = "created",
            entitlementsEnforced = ListContactCenterAgentsTool.IsEntitlementsEnabled(services),
            agent = describer.Describe(agent),
        });
    }

    private static List<AgentSkill> ReadSkills(ContactCenterToolArguments reader)
    {
        var rows = reader.NestedList("skills");

        if (rows is null)
        {
            return null;
        }

        var skills = new List<AgentSkill>();

        foreach (var row in rows)
        {
            var skillId = row.GetString("skillId");

            if (skillId is null)
            {
                reader.Errors.Add("Each skill needs a skillId.");

                continue;
            }

            row.Int("proficiency", nameof(AgentSkill.Proficiency), AgentSkill.MinimumProficiency, AgentSkill.MaximumProficiency);
            skills.Add(new AgentSkill { SkillId = skillId, Proficiency = row.GetInt("proficiency") ?? AgentSkill.DefaultProficiency });
        }

        return skills;
    }

    private static List<AgentQueueMembership> ReadMemberships(ContactCenterToolArguments reader)
    {
        var rows = reader.NestedList("queueMemberships");

        if (rows is null)
        {
            return null;
        }

        var memberships = new List<AgentQueueMembership>();

        foreach (var row in rows)
        {
            var queueId = row.GetString("queueId");

            if (queueId is null)
            {
                reader.Errors.Add("Each queue membership needs a queueId.");

                continue;
            }

            row.Int("priority", nameof(AgentQueueMembership.Priority), minimum: 0);
            row.Int("delaySeconds", nameof(AgentQueueMembership.DelaySeconds), minimum: 0);
            memberships.Add(new AgentQueueMembership
            {
                QueueId = queueId,
                Priority = row.GetInt("priority") ?? 0,
                DelaySeconds = row.GetInt("delaySeconds") ?? 0,
            });
        }

        return memberships;
    }
}
