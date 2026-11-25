using CrestApps.OrchardCore.Teams.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// Shows and edits a Workforce workflow event: the teams it starts the workflow for.
/// </summary>
/// <typeparam name="TActivity">The event.</typeparam>
public sealed class WorkforceEventActivityDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, WorkforceEventActivityViewModel>
    where TActivity : WorkforceEventActivity
{
    private readonly ITeamManager _teamManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceEventActivityDisplayDriver{TActivity}"/> class.
    /// </summary>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceEventActivityDisplayDriver(ITeamManager teamManager, IStringLocalizer<WorkforceEventActivityDisplayDriver<TActivity>> stringLocalizer)
    {
        _teamManager = teamManager;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(TActivity activity, BuildEditorContext context)
        => Initialize<WorkforceEventActivityViewModel>($"{typeof(TActivity).Name}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Event"], "10"));

    /// <inheritdoc />
    protected override async ValueTask EditActivityAsync(TActivity activity, WorkforceEventActivityViewModel model)
    {
        model.TeamIds = [.. activity.TeamIds];
        model.TeamOptions = (await _teamManager.ListTreeAsync())
            .Select(entry => new SelectListItem(new string('\u00A0', entry.Depth * 3) + entry.Team.Name, entry.Team.ItemId, model.TeamIds.Contains(entry.Team.ItemId)))
            .ToList();
    }

    /// <inheritdoc />
    protected override void UpdateActivity(WorkforceEventActivityViewModel model, TActivity activity)
    {
        activity.TeamIds = (model.TeamIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
    }
}
