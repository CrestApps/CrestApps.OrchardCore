using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Creates or updates a queue through the queue manager, whose handler validates it as it validates the queue editor's
/// post, after the editor's own rules are applied.
/// </summary>
internal sealed class SaveContactCenterQueueTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveContactCenterQueue";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": { "type": "string", "description": "The ID of the queue to update. Leave it out to create a queue." },
        "name": { "type": "string", "description": "The queue's name. Required when creating." },
        "description": { "type": "string", "description": "A description of the queue." },
        "queueGroupId": { "type": "string", "description": "The ID of the queue group (see listRecords with the kind contactCenterQueueGroup). An empty text removes the queue from its group." },
        "enabled": { "type": "boolean", "description": "Whether the queue routes work. Disable a queue rather than emptying its settings." },
        "defaultPriority": { "type": "string", "enum": ["Lowest", "Low", "Normal", "High", "Highest"], "description": "The priority given to work entering the queue." },
        "routingStrategy": { "type": "string", "enum": ["LongestIdle", "RoundRobin", "LeastBusy"], "description": "How an agent is picked for the next item." },
        "preferStickyAgent": { "type": "boolean", "description": "Whether a returning customer is offered first to the agent who last served them." },
        "enableSlaAging": { "type": "boolean", "description": "Whether waiting items gain priority as they near the SLA threshold." },
        "slaThresholdSeconds": { "type": "integer", "minimum": 0, "description": "The service level target: seconds an item may wait before it is late." },
        "firstResponseTargetSeconds": { "type": "integer", "minimum": 0, "description": "The first-response target in seconds for written channels; 0 for none." },
        "reservationTimeoutSeconds": { "type": "integer", "minimum": 1, "description": "Seconds an agent has to accept an offer." },
        "unansweredOfferAction": { "type": "string", "enum": ["Requeue", "Voicemail", "Reject"], "description": "What happens when an offer is not answered." },
        "businessHoursCalendarId": { "type": "string", "description": "The ID of the business-hours calendar (see listRecords with the kind businessHoursCalendar). An empty text makes the queue always open." },
        "afterHoursAction": { "type": "string", "enum": ["HoldInQueue", "Overflow"], "description": "What happens to work that arrives while the queue is closed." },
        "inboundChannelEndpointId": { "type": "string", "description": "The address the queue answers directly. Only when the Inbound Entry Points feature is off; with it on, numbers are routed by entry points." },
        "requiredSkills": { "type": "array", "items": { "type": "string" }, "description": "Skill names an agent must have. Replaces the whole list." },
        "skillRequirements": {
          "type": "array",
          "description": "Skill requirements with proficiency. Replaces the whole list.",
          "items": {
            "type": "object",
            "properties": {
              "skillId": { "type": "string", "description": "The skill name." },
              "minimumProficiency": { "type": "integer", "minimum": 1, "maximum": 5, "description": "The lowest proficiency accepted, 1 to 5." },
              "required": { "type": "boolean", "description": "true when the skill is required; false when it is only preferred." },
              "relaxAfterSeconds": { "type": "integer", "minimum": 0, "description": "Seconds after which the requirement is dropped; 0 never." }
            },
            "required": ["skillId"],
            "additionalProperties": false
          }
        },
        "overflowQueueId": { "type": "string", "description": "The queue work overflows to. An empty text removes it." },
        "overflowAfterSeconds": { "type": "integer", "minimum": 0, "description": "Seconds before work overflows to overflowQueueId." },
        "overflowTargets": {
          "type": "array",
          "description": "Further overflow hops in order of delay. Replaces the whole list.",
          "items": {
            "type": "object",
            "properties": {
              "queueId": { "type": "string", "description": "The queue to overflow to." },
              "afterSeconds": { "type": "integer", "minimum": 0, "description": "Seconds of waiting before this hop." }
            },
            "required": ["queueId"],
            "additionalProperties": false
          }
        },
        "maxWaitSeconds": { "type": "integer", "minimum": 0, "description": "The longest wait in seconds before maxWaitAction applies; 0 for no limit." },
        "maxWaitAction": { "type": "string", "enum": ["None", "Voicemail", "Overflow"], "description": "What happens when the maximum wait is reached." },
        "maxQueueSize": { "type": "integer", "minimum": 0, "description": "The most items that may wait; 0 for no limit." },
        "queueFullAction": { "type": "string", "enum": ["None", "Voicemail", "Overflow"], "description": "What happens to new work when the queue is full." },
        "treatment": {
          "type": "object",
          "description": "What a waiting caller hears. Only the settings given are changed.",
          "properties": {
            "welcomeMessage": { "type": "string", "description": "The message played when a caller enters the queue." },
            "announcementIntervalSeconds": { "type": "integer", "minimum": 0, "description": "Seconds between announcements; 0 for none." },
            "announcePosition": { "type": "boolean", "description": "Whether the caller's position is announced." },
            "announceEstimatedWait": { "type": "boolean", "description": "Whether the estimated wait is announced." },
            "holdMusicMediaId": { "type": "string", "description": "The ID of the voice media clip played as hold music." },
            "callbackDtmfKey": { "type": "string", "description": "The telephone key (0-9, * or #) a caller presses to ask for a callback; empty for no callback offer." },
            "callbackOfferAfterSeconds": { "type": "integer", "minimum": 0, "description": "Seconds of waiting before the callback is offered." },
            "minimumEstimateSeconds": { "type": "integer", "minimum": 0, "description": "The shortest wait estimate announced." },
            "maximumEstimateSeconds": { "type": "integer", "minimum": 0, "description": "The longest wait estimate announced." },
            "averageHandleTimeSeconds": { "type": "integer", "minimum": 0, "description": "The average handle time used to estimate waits; 0 to measure it." }
          },
          "additionalProperties": false
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates a Contact Center queue. Only the fields given are changed on an update; lists given replace the whole list. Changes apply to live routing immediately, so disable a queue rather than emptying its settings. Read the queue first with getRecord (kind contactCenterQueue), which returns these field names.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.ManageQueues;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<IActivityQueueManager>();
        var reader = new ContactCenterToolArguments(arguments);
        var id = reader.GetString("id");
        var queue = id is null ? null : await manager.FindByIdAsync(id, cancellationToken);

        if (id is not null && queue is null)
        {
            return $"Nothing was saved: no queue has the ID '{id}'. Use listRecords with the kind contactCenterQueue to find it.";
        }

        if (reader.Has("inboundChannelEndpointId") && services.GetService<IContactCenterEntryPointManager>() is not null)
        {
            return "Nothing was saved: with the Inbound Entry Points feature on, a number is routed by its entry point, not by the queue. Use saveContactCenterEntryPoint instead of inboundChannelEndpointId.";
        }

        reader.Text("name", nameof(ActivityQueue.Name), required: true);
        reader.Text("description", nameof(ActivityQueue.Description));
        reader.Text("queueGroupId", nameof(ActivityQueue.QueueGroupId));
        reader.Bool("enabled", nameof(ActivityQueue.Enabled));
        reader.Choice<InteractionPriority>("defaultPriority", nameof(ActivityQueue.DefaultPriority));
        reader.Choice<QueueRoutingStrategy>("routingStrategy", nameof(ActivityQueue.RoutingStrategy));
        reader.Bool("preferStickyAgent", nameof(ActivityQueue.PreferStickyAgent));
        reader.Bool("enableSlaAging", nameof(ActivityQueue.EnableSlaAging));
        reader.Int("slaThresholdSeconds", nameof(ActivityQueue.SlaThresholdSeconds), minimum: 0);
        reader.Int("firstResponseTargetSeconds", nameof(ActivityQueue.FirstResponseTargetSeconds), minimum: 0);
        reader.Int("reservationTimeoutSeconds", nameof(ActivityQueue.ReservationTimeoutSeconds), minimum: 1);
        reader.Choice<UnansweredOfferAction>("unansweredOfferAction", nameof(ActivityQueue.UnansweredOfferAction));
        reader.Text("businessHoursCalendarId", nameof(ActivityQueue.BusinessHoursCalendarId));
        reader.Choice<QueueAfterHoursAction>("afterHoursAction", nameof(ActivityQueue.AfterHoursAction));
        reader.Text("inboundChannelEndpointId", nameof(ActivityQueue.InboundChannelEndpointId));
        reader.Text("overflowQueueId", nameof(ActivityQueue.OverflowQueueId));
        reader.Int("overflowAfterSeconds", nameof(ActivityQueue.OverflowAfterSeconds), minimum: 0);
        reader.Int("maxWaitSeconds", nameof(ActivityQueue.MaxWaitSeconds), minimum: 0);
        reader.Choice<QueueMaxWaitAction>("maxWaitAction", nameof(ActivityQueue.MaxWaitAction));
        reader.Int("maxQueueSize", nameof(ActivityQueue.MaxQueueSize), minimum: 0);
        reader.Choice<QueueMaxWaitAction>("queueFullAction", nameof(ActivityQueue.QueueFullAction));
        reader.IdList("requiredSkills", nameof(ActivityQueue.RequiredSkills));

        ReadSkillRequirements(reader);
        ReadOverflowTargets(reader);
        ReadTreatment(reader, queue?.Treatment);

        if (reader.Errors.Count > 0)
        {
            return "Nothing was saved: " + string.Join(" ", reader.Errors);
        }

        var missing = await FindMissingReferencesAsync(services, reader.Changes, cancellationToken);

        if (missing.Count > 0)
        {
            return "Nothing was saved: " + string.Join(" ", missing);
        }

        var result = await ContactCenterCatalogWriter.SaveAsync(manager, queue, reader.Changes, ActivityQueueEditorRules.Normalize, cancellationToken);

        if (!result.Succeeded)
        {
            return result.Message;
        }

        var describer = await ContactCenterQueueDescriber.CreateAsync(services, cancellationToken);

        return Json(new
        {
            saved = queue is null ? "created" : "updated",
            changed = reader.Changes.Select(change => change.Key).ToList(),
            queue = await describer.DescribeAsync(result.Record),
        });
    }

    private static void ReadSkillRequirements(ContactCenterToolArguments reader)
    {
        var rows = reader.NestedList("skillRequirements");

        if (rows is null)
        {
            return;
        }

        var requirements = new List<QueueSkillRequirement>();

        foreach (var row in rows)
        {
            var skillId = row.GetString("skillId");

            if (skillId is null)
            {
                reader.Errors.Add("Each skill requirement needs a skillId.");

                continue;
            }

            row.Int("minimumProficiency", nameof(QueueSkillRequirement.MinimumProficiency), AgentSkill.MinimumProficiency, AgentSkill.MaximumProficiency);
            row.Bool("required", nameof(QueueSkillRequirement.Required));
            row.Int("relaxAfterSeconds", nameof(QueueSkillRequirement.RelaxAfterSeconds), minimum: 0);

            requirements.Add(new QueueSkillRequirement
            {
                SkillId = skillId,
                MinimumProficiency = row.GetInt("minimumProficiency") ?? AgentSkill.DefaultProficiency,
                Required = row.GetBool("required") ?? true,
                RelaxAfterSeconds = row.GetInt("relaxAfterSeconds"),
            });
        }

        reader.Changes[nameof(ActivityQueue.SkillRequirements)] = JsonSerializer.SerializeToNode(requirements);
    }

    private static void ReadOverflowTargets(ContactCenterToolArguments reader)
    {
        var rows = reader.NestedList("overflowTargets");

        if (rows is null)
        {
            return;
        }

        var targets = new List<QueueOverflowTarget>();

        foreach (var row in rows)
        {
            var queueId = row.GetString("queueId");

            if (queueId is null)
            {
                reader.Errors.Add("Each overflow target needs a queueId.");

                continue;
            }

            row.Int("afterSeconds", nameof(QueueOverflowTarget.AfterSeconds), minimum: 0);
            targets.Add(new QueueOverflowTarget { QueueId = queueId, AfterSeconds = row.GetInt("afterSeconds") ?? 0 });
        }

        reader.Changes[nameof(ActivityQueue.OverflowTargets)] = JsonSerializer.SerializeToNode(targets);
    }

    // The treatment is stored as one object; the settings given are laid over the current ones so the rest are kept.
    private static void ReadTreatment(ContactCenterToolArguments reader, QueueTreatmentSettings current)
    {
        var treatment = reader.Nested("treatment");

        if (treatment is null)
        {
            return;
        }

        treatment.Text("welcomeMessage", nameof(QueueTreatmentSettings.WelcomeMessage));
        treatment.Int("announcementIntervalSeconds", nameof(QueueTreatmentSettings.AnnouncementIntervalSeconds), minimum: 0);
        treatment.Bool("announcePosition", nameof(QueueTreatmentSettings.AnnouncePosition));
        treatment.Bool("announceEstimatedWait", nameof(QueueTreatmentSettings.AnnounceEstimatedWait));
        treatment.Text("holdMusicMediaId", nameof(QueueTreatmentSettings.HoldMusicMediaId));
        treatment.Text("callbackDtmfKey", nameof(QueueTreatmentSettings.CallbackDtmfKey));
        treatment.Int("callbackOfferAfterSeconds", nameof(QueueTreatmentSettings.CallbackOfferAfterSeconds), minimum: 0);
        treatment.Int("minimumEstimateSeconds", nameof(QueueTreatmentSettings.MinimumEstimateSeconds), minimum: 0);
        treatment.Int("maximumEstimateSeconds", nameof(QueueTreatmentSettings.MaximumEstimateSeconds), minimum: 0);
        treatment.Int("averageHandleTimeSeconds", nameof(QueueTreatmentSettings.AverageHandleTimeSeconds), minimum: 0);

        var merged = JsonSerializer.SerializeToNode(current ?? new QueueTreatmentSettings()) as JsonObject ?? [];

        foreach (var (key, value) in treatment.Changes)
        {
            merged[key] = value?.DeepClone();
        }

        reader.Changes[nameof(ActivityQueue.Treatment)] = merged;
    }

    // The editor offers only existing records in its lists, so a typed ID that names nothing is refused here.
    private static async Task<List<string>> FindMissingReferencesAsync(IServiceProvider services, JsonObject changes, CancellationToken cancellationToken)
    {
        var missing = new List<string>();
        var queueNames = await ContactCenterSetupLookups.GetQueueNamesAsync(services, cancellationToken);

        if (changes[nameof(ActivityQueue.BusinessHoursCalendarId)]?.GetValue<string>() is { } calendarId &&
            !(await ContactCenterSetupLookups.GetCalendarNamesAsync(services, cancellationToken)).ContainsKey(calendarId))
        {
            missing.Add($"No business-hours calendar has the ID '{calendarId}'. Use listRecords with the kind businessHoursCalendar to find it.");
        }

        if (changes[nameof(ActivityQueue.OverflowQueueId)]?.GetValue<string>() is { } overflowQueueId && !queueNames.ContainsKey(overflowQueueId))
        {
            missing.Add($"No queue has the ID '{overflowQueueId}' to overflow to.");
        }

        foreach (var target in changes[nameof(ActivityQueue.OverflowTargets)]?.AsArray() ?? [])
        {
            var targetId = target?[nameof(QueueOverflowTarget.QueueId)]?.GetValue<string>();

            if (targetId is not null && !queueNames.ContainsKey(targetId))
            {
                missing.Add($"No queue has the ID '{targetId}' to overflow to.");
            }
        }

        return missing;
    }
}
