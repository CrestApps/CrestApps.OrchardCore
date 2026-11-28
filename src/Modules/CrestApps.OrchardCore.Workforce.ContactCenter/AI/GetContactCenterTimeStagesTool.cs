using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.TimeTracking;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using static CrestApps.OrchardCore.Workforce.ContactCenter.AI.ContactCenterWorkforceTools;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.AI;

/// <summary>
/// Gets the Time Tracker stage of each agent state and not-ready reason, as the Contact Center Stages page shows them.
/// </summary>
internal sealed class GetContactCenterTimeStagesTool : ContactCenterWorkforceToolBase
{
    public const string ToolName = "getContactCenterTimeStages";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {},
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Gets which Time Tracker stage each contact center agent state (available, on a call, wrap-up...) and each not-ready reason is recorded in, and the stages that can be chosen. A reason's stage comes before its state's; a state or reason with no stage isn't tracked.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => TimeTrackerPermissions.ManageTimeTrackerSettings;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var settings = (await services.GetRequiredService<ISiteService>().GetSiteSettingsAsync()).GetOrCreate<ContactCenterTimeStages>();
        var stages = (await services.GetRequiredService<ITimeStageManager>().GetAllAsync(cancellationToken)).ToList();
        var stageNames = stages.ToDictionary(stage => stage.ItemId, stage => stage.Name, StringComparer.Ordinal);
        var reasons = (await services.GetRequiredService<IAgentStateReasonCodeManager>().GetAllAsync(cancellationToken))
            .Where(reason => reason.Enabled)
            .OrderBy(reason => reason.SortOrder)
            .ThenBy(reason => reason.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        object Stage(string stageId)
            => string.IsNullOrEmpty(stageId) ? null : new { id = stageId, name = stageNames.GetValueOrDefault(stageId) };

        return ToJson(new
        {
            states = Enum.GetValues<AgentPresenceStatus>().Where(state => state != AgentPresenceStatus.Offline).Select(state => new
            {
                state = state.ToString(),
                stage = Stage(settings.States.GetValueOrDefault(state.ToString())),
            }),
            reasons = reasons.Select(reason => new
            {
                reason = reason.Name,
                appliesTo = reason.AppliesTo.ToString(),
                stage = Stage(settings.Reasons.GetValueOrDefault(reason.Name)),
            }),
            stagesToChoose = stages
                .Where(stage => stage.Enabled)
                .OrderBy(stage => stage.Position)
                .ThenBy(stage => stage.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(stage => new { id = stage.ItemId, name = stage.Name }),
        });
    }
}
