using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.TimeTracker.AI;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TimeTracker;

/// <summary>
/// Adds the Time Tracker AI tools when the AI feature is on: a person's own clock (status, clock in, switch, clock out),
/// and for managers the time entries list, totals, adding, changing and deleting entries. Time stages and categories are
/// exposed read-only to the generic record tools. Each tool works only for a signed-in person with the permission of the
/// matching page.
/// </summary>
[Feature(TimeTrackerConstants.Features.TimeTracker)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetMyTimeStatusTool>(GetMyTimeStatusTool.ToolName)
            .WithTitle(S["My time status"])
            .WithDescription(S["Says whether the user is clocked in, in which stage and since when, with today's entries, the week's hours by category and the stages they can switch to."])
            .WithCategory(S["Time Tracker"])
            .Selectable();

        services.AddCoreAITool<ClockMyTimeTool>(ClockMyTimeTool.ToolName)
            .WithTitle(S["Clock my time"])
            .WithDescription(S["Clocks the user in or switches them to another stage (such as Work, Break or Lunch), or clocks them out. When the Workforce time clock keeps their time, it punches there, with that clock's rules."])
            .WithCategory(S["Time Tracker"])
            .Selectable();

        services.AddCoreAITool<ListTimeEntriesTool>(ListTimeEntriesTool.ToolName)
            .WithTitle(S["List time entries"])
            .WithDescription(S["Lists anyone's time entries filtered by user, stage and a date range, with who changed each and why."])
            .WithCategory(S["Time Tracker"])
            .Selectable();

        services.AddCoreAITool<GetTimeTotalsTool>(GetTimeTotalsTool.ToolName)
            .WithTitle(S["Get time totals"])
            .WithDescription(S["Totals tracked time per person, optionally per day, by category (such as Payable or Billable) and by stage for a date range. Use it for \"how many hours did X work\"."])
            .WithCategory(S["Time Tracker"])
            .Selectable();

        services.AddCoreAITool<SaveTimeEntryTool>(SaveTimeEntryTool.ToolName)
            .WithTitle(S["Save time entry"])
            .WithDescription(S["Adds a time entry for a user or changes one's stage, start, end or note. A reason is required and is kept in the entry's history."])
            .WithCategory(S["Time Tracker"])
            .Selectable();

        services.AddCoreAITool<DeleteTimeEntryTool>(DeleteTimeEntryTool.ToolName)
            .WithTitle(S["Delete time entry"])
            .WithDescription(S["Deletes a time entry, with a reason."])
            .WithCategory(S["Time Tracker"])
            .Selectable()
            .RequireApproval();

        // The Stages and Categories pages bind their own forms (no display driver), so the records are read-only here.
        services.AddAIRecordKind<TimeStage>("timeStage", kind => kind
            .WithTitle(S["Time stage"])
            .WithDescription(S["a Time Tracker stage (Work, Break, Lunch...) people clock in to, with the categories its time counts toward and whether people may choose it themselves; read-only, changed on the Stages page"])
            .WithPermission(TimeTrackerPermissions.ManageTimeTrackerSettings)
            .UseCatalogManager<ITimeStageManager>()
            .WithProjection(stage => new
            {
                id = stage.ItemId,
                name = stage.Name,
                code = stage.Code,
                color = stage.Color,
                position = stage.Position,
                categoryIds = (stage.CategoryIds ?? new List<string>()).ToList(),
                selfService = stage.SelfService,
                enabled = stage.Enabled,
            })
            .ReadOnly());

        services.AddAIRecordKind<TimeCategory>("timeCategory", kind => kind
            .WithTitle(S["Time category"])
            .WithDescription(S["a Time Tracker category (Payable, Billable, Vacation...) that stages count time toward, with the factor each minute counts; read-only, changed on the Categories page"])
            .WithPermission(TimeTrackerPermissions.ManageTimeTrackerSettings)
            .UseCatalogManager<ITimeCategoryManager>()
            .WithProjection(category => new
            {
                id = category.ItemId,
                name = category.Name,
                code = category.Code,
                color = category.Color,
                position = category.Position,
                factor = category.Factor,
                enabled = category.Enabled,
            })
            .ReadOnly());
    }
}
