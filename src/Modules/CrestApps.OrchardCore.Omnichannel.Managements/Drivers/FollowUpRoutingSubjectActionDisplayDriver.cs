using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Security.Services;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Edits the follow-up section of a "Try again" or "New activity" subject action: the campaign of the follow-up, how it
/// is dialed, and who owns it.
/// </summary>
/// <remarks>
/// How the follow-up is dialed and who owns it depend on each other: a follow-up a dialer profile calls is offered to
/// whichever agent signed in to the campaign is free, so it has no owner of its own. They are edited together, in that
/// order, and the owner choices are hidden when a dialer profile is picked.
/// </remarks>
internal sealed class FollowUpRoutingSubjectActionDisplayDriver : DisplayDriver<SubjectAction>
{
    private readonly BulkActivityAdminFormOptionsProvider _formOptions;
    private readonly ISession _session;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly IRoleService _roleService;
    private readonly ITeamManager _teamManager;

    internal readonly IStringLocalizer S;

    public FollowUpRoutingSubjectActionDisplayDriver(
        BulkActivityAdminFormOptionsProvider formOptions,
        ISession session,
        IDisplayNameProvider displayNameProvider,
        IRoleService roleService,
        IEnumerable<ITeamManager> teamManagers,
        IStringLocalizer<FollowUpRoutingSubjectActionDisplayDriver> stringLocalizer)
    {
        _formOptions = formOptions;
        _session = session;
        _displayNameProvider = displayNameProvider;
        _roleService = roleService;
        _teamManager = teamManagers.FirstOrDefault();
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(SubjectAction action, BuildEditorContext context)
    {
        if (!CreatesFollowUp(action))
        {
            return null;
        }

        return Initialize<FollowUpRoutingSubjectActionViewModel>("FollowUpRoutingSubjectAction_Edit", async model =>
        {
            if (!action.TryGet<FollowUpRoutingMetadata>(out var routing))
            {
                routing = new FollowUpRoutingMetadata();
            }

            if (!action.TryGet<FollowUpOwnerPoolMetadata>(out var pool))
            {
                pool = new FollowUpOwnerPoolMetadata();
            }

            var (assignmentType, normalizedUserName) = ReadAssignment(action);

            model.CampaignId = routing.CampaignId;
            model.DialWith = routing.Target switch
            {
                FollowUpRoutingTarget.Agent => FollowUpRoutingSubjectActionViewModel.DialWithNone,
                FollowUpRoutingTarget.Dialer => routing.DialerProfileId,
                _ => FollowUpRoutingSubjectActionViewModel.DialWithInherited,
            };
            model.AssignTo = SubjectActionOwnerAssignmentTypeResolver.Resolve(assignmentType, normalizedUserName);
            model.NormalizedUserName = normalizedUserName;
            model.PoolSource = pool.Source;
            model.PoolTeamId = pool.TeamId;
            model.PoolRoleNames = pool.RoleNames ?? [];
            model.PoolUserIds = pool.UserIds ?? [];
            model.PoolStrategy = pool.Strategy;
            model.PoolAvailable = _teamManager is not null;

            model.Campaigns = await _formOptions.GetCampaignOptionsAsync(routing.CampaignId, S["Keep its campaign"]);
            model.DialerProfiles = (await _formOptions.GetDialerProfileOptionsAsync(routing.DialerProfileId, S["Choose a dialer profile"]))
                .Where(option => !string.IsNullOrEmpty(option.Value))
                .ToList();

            if (!string.IsNullOrWhiteSpace(normalizedUserName))
            {
                var user = await _session.Query<User, UserIndex>(index => index.NormalizedUserName == normalizedUserName).FirstOrDefaultAsync();

                model.SelectedUsers =
                [
                    new SelectListItem(user is null ? S["(Unknown user)"] : await _displayNameProvider.GetAsync(user), normalizedUserName),
                ];
            }

            if (_teamManager is not null)
            {
                model.Teams = (await _teamManager.GetAllAsync())
                    .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(team => new SelectListItem(team.Name, team.ItemId, string.Equals(team.ItemId, pool.TeamId, StringComparison.Ordinal)))
                    .ToList();
            }

            model.Roles = (await _roleService.GetRoleNamesAsync())
                .Where(role => !string.Equals(role, OrchardCoreConstants.Roles.Anonymous, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(role, OrchardCoreConstants.Roles.Authenticated, StringComparison.OrdinalIgnoreCase))
                .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
                .Select(role => new SelectListItem(role, role, model.PoolRoleNames.Contains(role, StringComparer.OrdinalIgnoreCase)))
                .ToList();
        }).Location("Content:6");
    }

    public override async Task<IDisplayResult> UpdateAsync(SubjectAction action, UpdateEditorContext context)
    {
        if (!CreatesFollowUp(action))
        {
            return null;
        }

        var model = new FollowUpRoutingSubjectActionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var dialWith = model.DialWith?.Trim() ?? string.Empty;
        var dialedByProfile = dialWith.Length > 0 && dialWith != FollowUpRoutingSubjectActionViewModel.DialWithNone;

        // A dialer profile offers the follow-up to whichever agent is free, so it keeps no owner of its own.
        var assignTo = dialedByProfile ? SubjectActionOwnerAssignmentType.SameOwner : model.AssignTo;
        var normalizedUserName = assignTo == SubjectActionOwnerAssignmentType.SpecificOwner ? model.NormalizedUserName?.Trim() : null;

        // The rules (a dialer profile that exists, a user or a pool to pick from) live on the action's handler, so the
        // editor, a recipe and a deployment plan all enforce them.
        action.Put(new FollowUpRoutingMetadata
        {
            Target = dialWith.Length == 0
                ? FollowUpRoutingTarget.SameAsBefore
                : dialedByProfile ? FollowUpRoutingTarget.Dialer : FollowUpRoutingTarget.Agent,
            CampaignId = string.IsNullOrWhiteSpace(model.CampaignId) ? null : model.CampaignId.Trim(),
            DialerProfileId = dialedByProfile ? dialWith : null,
        });

        action.Put(new FollowUpOwnerPoolMetadata
        {
            Source = model.PoolSource,
            TeamId = assignTo == SubjectActionOwnerAssignmentType.Pool && model.PoolSource == FollowUpOwnerPoolSource.Team ? model.PoolTeamId?.Trim() : null,
            RoleNames = assignTo == SubjectActionOwnerAssignmentType.Pool && model.PoolSource == FollowUpOwnerPoolSource.Roles ? Clean(model.PoolRoleNames) : [],
            UserIds = assignTo == SubjectActionOwnerAssignmentType.Pool && model.PoolSource == FollowUpOwnerPoolSource.Users ? Clean(model.PoolUserIds) : [],
            Strategy = model.PoolStrategy,
        });

        WriteAssignment(action, assignTo, normalizedUserName);

        return Edit(action, context);
    }

    // The owner is stored on the action type's own metadata, where the executor and older actions read it.
    private static (SubjectActionOwnerAssignmentType AssignmentType, string NormalizedUserName) ReadAssignment(SubjectAction action)
    {
        if (IsTryAgain(action))
        {
            return action.TryGet<TryAgainActionMetadata>(out var tryAgain)
                ? (tryAgain.AssignmentType, tryAgain.NormalizedUserName)
                : (SubjectActionOwnerAssignmentType.SameOwner, null);
        }

        return action.TryGet<NewActivityActionMetadata>(out var newActivity)
            ? (newActivity.AssignmentType, newActivity.NormalizedUserName)
            : (SubjectActionOwnerAssignmentType.SameOwner, null);
    }

    private static void WriteAssignment(SubjectAction action, SubjectActionOwnerAssignmentType assignmentType, string normalizedUserName)
    {
        if (IsTryAgain(action))
        {
            if (!action.TryGet<TryAgainActionMetadata>(out var tryAgain))
            {
                tryAgain = new TryAgainActionMetadata();
            }

            tryAgain.AssignmentType = assignmentType;
            tryAgain.NormalizedUserName = normalizedUserName;
            action.Put(tryAgain);

            return;
        }

        if (!action.TryGet<NewActivityActionMetadata>(out var newActivity))
        {
            newActivity = new NewActivityActionMetadata();
        }

        newActivity.AssignmentType = assignmentType;
        newActivity.NormalizedUserName = normalizedUserName;
        action.Put(newActivity);
    }

    private static bool IsTryAgain(SubjectAction action)
        => string.Equals(action.Source, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.OrdinalIgnoreCase);

    private static bool CreatesFollowUp(SubjectAction action)
        => IsTryAgain(action) ||
            string.Equals(action.Source, OmnichannelConstants.ActionTypes.NewActivity, StringComparison.OrdinalIgnoreCase);

    private static string[] Clean(IEnumerable<string> values)
        => (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).ToArray();
}
