using CrestApps.Core;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.ViewModels;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.TimeTracker;

/// <summary>
/// The Time Tracker stage a desk's ticket work is recorded in, on the desk's editor.
/// </summary>
[RequireFeatures(TimeTrackerConstants.Features.TimeTracker)]
internal sealed class TicketDeskTimeDisplayDriver : DisplayDriver<TicketDesk>
{
    private readonly ITimeStageManager _stages;

    internal readonly IStringLocalizer S;

    public TicketDeskTimeDisplayDriver(ITimeStageManager stages, IStringLocalizer<TicketDeskTimeDisplayDriver> stringLocalizer)
    {
        _stages = stages;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TicketDesk desk, BuildEditorContext context)
        => Initialize<TicketDeskTimeViewModel>("TicketDeskTime_Edit", async model =>
        {
            var current = desk.TryGet<TicketDeskTimeSettings>(out var settings) ? settings : new TicketDeskTimeSettings();

            model.StageId = current.StageId;
            model.NotBilledStageId = current.NotBilledStageId;
            model.Stages = [.. (await _stages.GetAllAsync())
                .Where(stage => stage.Enabled || stage.ItemId == current.StageId || stage.ItemId == current.NotBilledStageId)
                .OrderBy(stage => stage.Position)
                .ThenBy(stage => stage.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(stage => new SelectListItem(stage.Name, stage.ItemId))];
        }).Location(l => l.Zone("Content", "5").Card(S["Time Tracking"], "70"));

    public override async Task<IDisplayResult> UpdateAsync(TicketDesk desk, UpdateEditorContext context)
    {
        var model = new TicketDeskTimeViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var stageId = string.IsNullOrWhiteSpace(model.StageId) ? null : model.StageId;
        var notBilledStageId = stageId is null || string.IsNullOrWhiteSpace(model.NotBilledStageId) ? null : model.NotBilledStageId;
        var valid = true;

        if (stageId is not null && await _stages.FindByIdAsync(stageId) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.StageId), S["Choose a stage that exists."]);
            valid = false;
        }

        if (notBilledStageId is not null && (notBilledStageId == stageId || await _stages.FindByIdAsync(notBilledStageId) is null))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.NotBilledStageId), S["Choose another stage that exists for time not billed."]);
            valid = false;
        }

        if (valid)
        {
            desk.Put(new TicketDeskTimeSettings { StageId = stageId, NotBilledStageId = notBilledStageId });
        }

        return Edit(desk, context);
    }
}
