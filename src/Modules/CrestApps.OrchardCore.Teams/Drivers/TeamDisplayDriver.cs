using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Teams.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Teams.Drivers;

/// <summary>
/// The generic section of the team editor: name, kind, parent, description, time zone, linked role and whether the team
/// is in use. Modules that build on teams add their own sections with drivers of their own.
/// </summary>
internal sealed class TeamDisplayDriver : DisplayDriver<Team>
{
    private readonly ITeamManager _teamManager;
    private readonly ITimeZoneSelectListProvider _timeZoneSelectListProvider;

    internal readonly IStringLocalizer S;

    public TeamDisplayDriver(
        ITeamManager teamManager,
        ITimeZoneSelectListProvider timeZoneSelectListProvider,
        IStringLocalizer<TeamDisplayDriver> stringLocalizer)
    {
        _teamManager = teamManager;
        _timeZoneSelectListProvider = timeZoneSelectListProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(Team team, BuildEditorContext context)
    {
        return Initialize<TeamEditViewModel>("TeamFields_Edit", async model =>
        {
            model.Name = team.Name;
            model.Kind = team.Kind.ToString();
            model.ParentId = team.ParentId;
            model.Description = team.Description;
            model.LinkedRoleName = team.LinkedRoleName;
            model.TimeZoneId = team.TimeZoneId;
            model.Enabled = team.Enabled;

            var tree = await _teamManager.ListTreeAsync();
            var excluded = context.IsNew || string.IsNullOrEmpty(team.ItemId)
                ? new HashSet<string>(StringComparer.Ordinal)
                : (await _teamManager.GetSubtreeIdsAsync(team.ItemId)).ToHashSet(StringComparer.Ordinal);

            model.ParentOptions = tree
                .Where(item => !excluded.Contains(item.Team.ItemId))
                .Select(item => new SelectListItem(new string(' ', item.Depth * 4) + item.Team.Name, item.Team.ItemId, item.Team.ItemId == model.ParentId))
                .ToList();
            model.KindOptions = KindOptions(model.Kind);
            model.TimeZoneOptions = await _timeZoneSelectListProvider.GetTimeZoneOptionsAsync(model.TimeZoneId);
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(Team team, UpdateEditorContext context)
    {
        var model = new TeamEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        team.Name = model.Name;
        team.Kind = Enum.TryParse<TeamKind>(model.Kind, out var kind) ? kind : TeamKind.Team;
        team.ParentId = model.ParentId;
        team.Description = model.Description;
        team.LinkedRoleName = model.LinkedRoleName;
        team.TimeZoneId = model.TimeZoneId;
        team.Enabled = model.Enabled;

        return Edit(team, context);
    }

    private List<SelectListItem> KindOptions(string selected)
    {
        return
        [
            new SelectListItem(S["Organization"], nameof(TeamKind.Organization), selected == nameof(TeamKind.Organization)),
            new SelectListItem(S["Region"], nameof(TeamKind.Region), selected == nameof(TeamKind.Region)),
            new SelectListItem(S["Site"], nameof(TeamKind.Site), selected == nameof(TeamKind.Site)),
            new SelectListItem(S["Department"], nameof(TeamKind.Department), selected == nameof(TeamKind.Department)),
            new SelectListItem(S["Team"], nameof(TeamKind.Team), selected == nameof(TeamKind.Team) || string.IsNullOrEmpty(selected)),
            new SelectListItem(S["Crew"], nameof(TeamKind.Crew), selected == nameof(TeamKind.Crew)),
        ];
    }
}
