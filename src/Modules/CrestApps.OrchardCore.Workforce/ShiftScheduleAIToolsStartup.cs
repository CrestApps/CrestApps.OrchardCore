using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Workforce.AI;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Adds the shift schedule AI tools: listing, reading, starting, filling, editing, publishing and deleting team
/// schedules, and changing published shifts. They work only for a signed-in person with the permission of the Shift
/// Schedules pages, in the teams that person manages.
/// </summary>
[Feature(WorkforceConstants.Features.ShiftPlanning)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class ShiftScheduleAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShiftScheduleAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ShiftScheduleAIToolsStartup(IStringLocalizer<ShiftScheduleAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListShiftSchedulesTool>(ListShiftSchedulesTool.ToolName)
            .WithTitle(S["List shift schedules"])
            .WithDescription(S["Lists the schedule periods of the teams the user manages, with team, dates, name and whether each is a draft or published."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<GetShiftScheduleTool>(GetShiftScheduleTool.ToolName)
            .WithTitle(S["Get shift schedule"])
            .WithDescription(S["Gets a schedule period: its draft shifts by worker and day, the shifts nobody has, where coverage is short or over, the labor rule problems and, when asked, the log of changes made after publishing."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<CreateShiftScheduleTool>(CreateShiftScheduleTool.ToolName)
            .WithTitle(S["Create shift schedule"])
            .WithDescription(S["Starts a draft schedule for a team from a start date for a number of weeks. Nothing is visible to workers until it is published."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<SaveDraftShiftTool>(SaveDraftShiftTool.ToolName)
            .WithTitle(S["Save draft shift"])
            .WithDescription(S["Adds or changes a shift in a draft schedule: the worker (or nobody, for an open shift), day, start and end, template, breaks and notes. Returns the labor rule problems the shift causes."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<DeleteDraftShiftTool>(DeleteDraftShiftTool.ToolName)
            .WithTitle(S["Delete draft shift"])
            .WithDescription(S["Removes a shift from a draft schedule. A published shift is removed with changePublishedShift instead."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<FillShiftScheduleTool>(FillShiftScheduleTool.ToolName)
            .WithTitle(S["Fill shift schedule"])
            .WithDescription(S["Fills a draft schedule in one of three ways: apply the team's rotations, copy the shifts of another period, or run the auto-scheduler against the coverage requirements. Returns how many shifts were added and what could not be covered."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<PublishShiftScheduleTool>(PublishShiftScheduleTool.ToolName)
            .WithTitle(S["Publish shift schedule"])
            .WithDescription(S["Publishes a draft schedule: shifts go on the team calendar, workers are notified, and shifts nobody has are offered as open shifts. Blocked by labor rule problems unless an override reason is given; run workforceExplainViolations first."])
            .WithCategory(S["Workforce"])
            .Selectable()
            .RequireApproval();

        services.AddCoreAITool<ChangePublishedShiftTool>(ChangePublishedShiftTool.ToolName)
            .WithTitle(S["Change published shift"])
            .WithDescription(S["Changes a published shift: new times, another worker, or removal, with a reason and whether the worker asked for it. The worker is notified and a late change can record predictability pay."])
            .WithCategory(S["Workforce"])
            .Selectable()
            .RequireApproval();

        services.AddCoreAITool<DeleteShiftScheduleTool>(DeleteShiftScheduleTool.ToolName)
            .WithTitle(S["Delete shift schedule"])
            .WithDescription(S["Deletes a draft schedule and its draft shifts. A published schedule cannot be deleted."])
            .WithCategory(S["Workforce"])
            .Selectable()
            .RequireApproval();
    }
}

/// <summary>
/// Adds the labor rule AI tools (the rule override log and predictability pay) and exposes the labor rule sets to the
/// generic record tools, read-only, since their editor binds its own form. The tools work only for a signed-in person
/// with the permission of the matching admin page, in the teams that person manages.
/// </summary>
[Feature(WorkforceConstants.Features.LaborRules)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class LaborRulesAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LaborRulesAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LaborRulesAIToolsStartup(IStringLocalizer<LaborRulesAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListLaborRuleOverridesTool>(ListLaborRuleOverridesTool.ToolName)
            .WithTitle(S["List labor rule overrides"])
            .WithDescription(S["Lists the labor rule overrides that were recorded: the rule, the worker, who overrode it, when and the reason."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<ListPredictabilityPayTool>(ListPredictabilityPayTool.ToolName)
            .WithTitle(S["List predictability pay"])
            .WithDescription(S["Lists the predictability pay entries raised by late schedule changes, filtered by team, worker and status, with the change that caused each and the hours owed."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<DecidePredictabilityPayTool>(DecidePredictabilityPayTool.ToolName)
            .WithTitle(S["Decide predictability pay"])
            .WithDescription(S["Approves a predictability pay entry so it is paid with the worker's timesheet, or waives it with a reason. A decision can't be undone."])
            .WithCategory(S["Workforce"])
            .Selectable()
            .RequireApproval();

        services.AddAIRecordKind<LaborRuleSet>("laborRuleSet", kind => kind
            .WithTitle(S["Labor rule set"])
            .WithDescription(S["a set of labor rules (rest between shifts, maximum hours, breaks, overtime, schedule notice...) with the teams, workers and employment types it applies to; read-only here, changed on the Labor Rules page"])
            .WithPermission(WorkforcePermissions.ManageLaborRules)
            .UseCatalogManager<ILaborRuleSetManager>()
            .WithProjection(set => new
            {
                id = set.ItemId,
                name = set.Name,
                description = set.Description,
                jurisdiction = set.Jurisdiction,
                enabled = set.Enabled,
                teamIds = set.TeamIds?.ToArray() ?? [],
                workerIds = set.WorkerIds?.ToArray() ?? [],
                employmentTypes = set.EmploymentTypes?.Select(type => type.ToString()).ToArray() ?? [],
                rules = set.Rules?.Select(rule => new
                {
                    id = rule.Id,
                    ruleKey = rule.RuleKey,
                    enforcement = rule.Enforcement.ToString(),
                    parameters = rule.Parameters is null
                        ? new Dictionary<string, string>(StringComparer.Ordinal)
                        : new Dictionary<string, string>(rule.Parameters, StringComparer.Ordinal),
                }).ToArray() ?? [],
            })
            .ReadOnly());
    }
}
