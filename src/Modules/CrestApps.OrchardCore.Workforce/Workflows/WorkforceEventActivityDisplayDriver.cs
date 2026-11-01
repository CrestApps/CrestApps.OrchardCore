using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceEventActivityDisplayDriver{TActivity}"/> class.
    /// </summary>
    /// <param name="teamManager">The team manager.</param>
    public WorkforceEventActivityDisplayDriver(ITeamManager teamManager)
    {
        _teamManager = teamManager;
    }

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
