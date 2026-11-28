using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.TimeTracking;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Entities;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using static CrestApps.OrchardCore.Workforce.ContactCenter.AI.ContactCenterWorkforceTools;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.AI;

/// <summary>
/// Sets the Time Tracker stage of agent states and not-ready reasons, as the Contact Center Stages page saves them. Only
/// the states and reasons given change; nothing is saved when one of them is wrong.
/// </summary>
internal sealed class SaveContactCenterTimeStagesTool : ContactCenterWorkforceToolBase
{
    public const string ToolName = "saveContactCenterTimeStages";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "states": {
          "type": "array",
          "description": "Optional. Agent states to set.",
          "items": {
            "type": "object",
            "properties": {
              "state": { "type": "string", "enum": ["Available", "Reserved", "Busy", "WrapUp", "Break", "RequestBreak", "Away", "DoNotDisturb", "Meeting", "Training", "AfterHoursUnavailable"], "description": "The agent state (Reserved is offered work, Busy is on a call or task)." },
              "stage": { "type": "string", "description": "The Time Tracker stage's ID or name; empty to stop tracking the state." }
            },
            "required": ["state", "stage"],
            "additionalProperties": false
          }
        },
        "reasons": {
          "type": "array",
          "description": "Optional. Not-ready reasons to set; a reason's stage comes before its state's.",
          "items": {
            "type": "object",
            "properties": {
              "reason": { "type": "string", "description": "The reason's name, as getContactCenterTimeStages lists it." },
              "stage": { "type": "string", "description": "The Time Tracker stage's ID or name; empty to use the state's stage." }
            },
            "required": ["reason", "stage"],
            "additionalProperties": false
          }
        }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Sets which Time Tracker stage agent states and not-ready reasons are recorded in. Only the states and reasons given change. Only existing stages are accepted (by ID or name); an empty stage stops tracking a state, or makes a reason use its state's stage.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => TimeTrackerPermissions.ManageTimeTrackerSettings;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var states = ReadPairs(arguments, "states", "state");
        var reasonRows = ReadPairs(arguments, "reasons", "reason");

        if (states.Count == 0 && reasonRows.Count == 0)
        {
            return "Give at least one state or reason with its stage (states, reasons).";
        }

        var stages = (await services.GetRequiredService<ITimeStageManager>().GetAllAsync(cancellationToken)).ToList();
        var reasonCodes = (await services.GetRequiredService<IAgentStateReasonCodeManager>().GetAllAsync(cancellationToken))
            .Where(reason => reason.Enabled)
            .ToList();
        var errors = new List<string>();
        var stateChanges = new List<(string Key, string StageId)>();
        var reasonChanges = new List<(string Key, string StageId)>();

        foreach (var (key, stage) in states)
        {
            if (!TryParseEnum<AgentPresenceStatus>(key, out var state) || state == AgentPresenceStatus.Offline)
            {
                errors.Add($"'{key}' is not an agent state that can have a stage.");

                continue;
            }

            if (ResolveStage(stages, stage, errors) is { } resolved)
            {
                stateChanges.Add((state.ToString(), resolved));
            }
        }

        foreach (var (key, stage) in reasonRows)
        {
            var code = reasonCodes.FirstOrDefault(reason => string.Equals(reason.Name, key, StringComparison.OrdinalIgnoreCase));

            if (code is null)
            {
                errors.Add($"No enabled not-ready reason is called '{key}'.");

                continue;
            }

            if (ResolveStage(stages, stage, errors) is { } resolved)
            {
                reasonChanges.Add((code.Name, resolved));
            }
        }

        if (errors.Count > 0)
        {
            return "Nothing was saved: " + string.Join(" ", errors) + " Use getContactCenterTimeStages for the states, reasons and stages.";
        }

        var siteService = services.GetRequiredService<ISiteService>();
        var site = await siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<ContactCenterTimeStages>();
        var stageIds = stages.Select(stage => stage.ItemId).ToHashSet(StringComparer.Ordinal);

        ContactCenterTimeStageMapping.Apply(settings.States, stateChanges, stageIds);
        ContactCenterTimeStageMapping.Apply(settings.Reasons, reasonChanges, stageIds);
        site.Put(settings);
        await siteService.UpdateSiteSettingsAsync(site);

        var names = stages.ToDictionary(stage => stage.ItemId, stage => stage.Name, StringComparer.Ordinal);

        return ToJson(new
        {
            saved = true,
            states = stateChanges.Select(change => new { state = change.Key, stage = change.StageId.Length == 0 ? null : names[change.StageId] }),
            reasons = reasonChanges.Select(change => new { reason = change.Key, stage = change.StageId.Length == 0 ? null : names[change.StageId] }),
        });
    }

    // Returns the stage's ID, an empty text to clear, or null with an error when no stage matches.
    private static string ResolveStage(List<TimeStage> stages, string stage, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            return string.Empty;
        }

        var match = stages.FirstOrDefault(item => item.ItemId == stage)
            ?? stages.FirstOrDefault(item => string.Equals(item.Name, stage, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            errors.Add($"No Time Tracker stage is called '{stage}'.");

            return null;
        }

        return match.ItemId;
    }

    private static List<(string Key, string Stage)> ReadPairs(AIFunctionArguments arguments, string name, string keyName)
    {
        var pairs = new List<(string, string)>();

        if (!Has(arguments, name))
        {
            return pairs;
        }

        var value = arguments[name];
        var element = value is JsonElement json ? json : JsonSerializer.SerializeToElement(value);

        if (element.ValueKind != JsonValueKind.Array)
        {
            return pairs;
        }

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var key = item.TryGetProperty(keyName, out var keyValue) && keyValue.ValueKind == JsonValueKind.String ? keyValue.GetString()?.Trim() : null;
            var stage = item.TryGetProperty("stage", out var stageValue) && stageValue.ValueKind == JsonValueKind.String ? stageValue.GetString()?.Trim() : null;

            if (!string.IsNullOrEmpty(key))
            {
                pairs.Add((key, stage));
            }
        }

        return pairs;
    }
}
