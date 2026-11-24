using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Edits the pool a "Try again" or "New activity" subject action picks the owner of its follow-up from, shown when its
/// assignment type is <see cref="SubjectActionOwnerAssignmentType.Pool"/>.
/// </summary>
internal sealed class FollowUpOwnerPoolSubjectActionDisplayDriver : DisplayDriver<SubjectAction>
{
    private readonly ITeamManager _teamManager;
    private readonly IRoleService _roleService;

    internal readonly IStringLocalizer S;

    public FollowUpOwnerPoolSubjectActionDisplayDriver(
        ITeamManager teamManager,
        IRoleService roleService,
        IStringLocalizer<FollowUpOwnerPoolSubjectActionDisplayDriver> stringLocalizer)
    {
        _teamManager = teamManager;
        _roleService = roleService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(SubjectAction action, BuildEditorContext context)
    {
        if (!CreatesFollowUp(action))
        {
            return null;
        }

        return Initialize<FollowUpOwnerPoolSubjectActionViewModel>("FollowUpOwnerPoolSubjectAction_Edit", async model =>
        {
            if (!action.TryGet<FollowUpOwnerPoolMetadata>(out var pool))
            {
                pool = new FollowUpOwnerPoolMetadata();
            }

            model.Source = pool.Source;
            model.TeamId = pool.TeamId;
            model.RoleNames = pool.RoleNames ?? [];
            model.UserIds = pool.UserIds ?? [];
            model.Strategy = pool.Strategy;
            model.Teams = (await _teamManager.GetAllAsync())
                .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
                .Select(team => new SelectListItem(team.Name, team.ItemId, string.Equals(team.ItemId, pool.TeamId, StringComparison.Ordinal)))
                .ToList();
            model.Roles = (await _roleService.GetRoleNamesAsync())
                .Where(role => !string.Equals(role, OrchardCoreConstants.Roles.Anonymous, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(role, OrchardCoreConstants.Roles.Authenticated, StringComparison.OrdinalIgnoreCase))
                .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
                .Select(role => new SelectListItem(role, role, model.RoleNames.Contains(role, StringComparer.OrdinalIgnoreCase)))
                .ToList();
        }).Location("Content:5.5");
    }

    public override async Task<IDisplayResult> UpdateAsync(SubjectAction action, UpdateEditorContext context)
    {
        if (!CreatesFollowUp(action))
        {
            return null;
        }

        var model = new FollowUpOwnerPoolSubjectActionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (UsesPool(action))
        {
            switch (model.Source)
            {
                case FollowUpOwnerPoolSource.Team when string.IsNullOrWhiteSpace(model.TeamId):
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.TeamId), S["Choose the team whose members get the follow-up."]);
                    break;

                case FollowUpOwnerPoolSource.Roles when Clean(model.RoleNames).Length == 0:
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.RoleNames), S["Choose at least one role."]);
                    break;

                case FollowUpOwnerPoolSource.Users when Clean(model.UserIds).Length == 0:
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.UserIds), S["Choose at least one user."]);
                    break;
            }
        }

        action.Put(new FollowUpOwnerPoolMetadata
        {
            Source = model.Source,
            TeamId = model.Source == FollowUpOwnerPoolSource.Team ? model.TeamId?.Trim() : null,
            RoleNames = model.Source == FollowUpOwnerPoolSource.Roles ? Clean(model.RoleNames) : [],
            UserIds = model.Source == FollowUpOwnerPoolSource.Users ? Clean(model.UserIds) : [],
            Strategy = model.Strategy,
        });

        return Edit(action, context);
    }

    // The assignment type is posted by the action's own editor, whose driver has already stored it.
    private static bool UsesPool(SubjectAction action)
        => action.TryGet<TryAgainActionMetadata>(out var tryAgain) && string.Equals(action.Source, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.OrdinalIgnoreCase)
            ? tryAgain.AssignmentType == SubjectActionOwnerAssignmentType.Pool
            : action.TryGet<NewActivityActionMetadata>(out var newActivity) && newActivity.AssignmentType == SubjectActionOwnerAssignmentType.Pool;

    private static bool CreatesFollowUp(SubjectAction action)
        => string.Equals(action.Source, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action.Source, OmnichannelConstants.ActionTypes.NewActivity, StringComparison.OrdinalIgnoreCase);

    private static string[] Clean(IEnumerable<string> values)
        => (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).ToArray();
}
