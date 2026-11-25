using System.ComponentModel.DataAnnotations;
using CrestApps.Core;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Users.Indexes;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Declares the rules a subject action must satisfy before it can be stored, whoever is writing it.
/// </summary>
internal sealed class SubjectActionHandler : CatalogEntryHandlerBase<SubjectAction>
{
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly ISession _session;
    private readonly IActivityDialerContributor _dialerContributor;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubjectActionHandler"/> class.
    /// </summary>
    /// <param name="subjectFlowSettingsService">The service used to confirm the subject an action names is configured.</param>
    /// <param name="session">The session used to confirm the owner an action names exists.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubjectActionHandler(
        ISubjectFlowSettingsService subjectFlowSettingsService,
        ISession session,
        IEnumerable<IActivityDialerContributor> dialerContributors,
        IStringLocalizer<SubjectActionHandler> stringLocalizer)
    {
        _dialerContributor = dialerContributors.FirstOrDefault();
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _session = session;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(InitializingContext<SubjectAction> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<SubjectAction> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<SubjectAction> context, CancellationToken cancellationToken = default)
    {
        var action = context.Model;

        if (string.IsNullOrWhiteSpace(action.DispositionId))
        {
            context.Result.Fail(new ValidationResult(S["Disposition is required."], [nameof(SubjectAction.DispositionId)]));
        }

        if (string.Equals(action.Source, OmnichannelConstants.ActionTypes.NewActivity, StringComparison.OrdinalIgnoreCase))
        {
            var metadata = action.GetOrCreate<NewActivityActionMetadata>();

            // An empty subject means the new activity keeps the subject type of the activity that raised the action, so
            // only a named subject has to be configured.
            if (!string.IsNullOrWhiteSpace(metadata.SubjectContentType) &&
                await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(metadata.SubjectContentType, cancellationToken) is null)
            {
                context.Result.Fail(new ValidationResult(S["The selected subject must be configured under Subject Flows before it can be used by a New Activity action."], [nameof(NewActivityActionMetadata.SubjectContentType)]));
            }

            await ValidateOwnerAsync(context, metadata.AssignmentType, metadata.NormalizedUserName);
            await ValidateFollowUpAsync(context, metadata.AssignmentType, cancellationToken);
        }
        else if (string.Equals(action.Source, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.OrdinalIgnoreCase))
        {
            var metadata = action.GetOrCreate<TryAgainActionMetadata>();

            await ValidateOwnerAsync(context, metadata.AssignmentType, metadata.NormalizedUserName);
            await ValidateFollowUpAsync(context, metadata.AssignmentType, cancellationToken);
        }
    }

    // The follow-up section: a dialer profile that exists, and a pool with somebody in it.
    private async Task ValidateFollowUpAsync(
        ValidatingContext<SubjectAction> context,
        SubjectActionOwnerAssignmentType assignmentType,
        CancellationToken cancellationToken)
    {
        var action = context.Model;

        if (action.TryGet<FollowUpRoutingMetadata>(out var routing) && routing.Target == FollowUpRoutingTarget.Dialer)
        {
            if (string.IsNullOrWhiteSpace(routing.DialerProfileId) ||
                _dialerContributor is null ||
                await _dialerContributor.FindByIdAsync(routing.DialerProfileId.Trim(), cancellationToken) is null)
            {
                context.Result.Fail(new ValidationResult(S["The selected dialer profile no longer exists."], ["DialWith"]));
            }
        }

        if (assignmentType != SubjectActionOwnerAssignmentType.Pool)
        {
            return;
        }

        var pool = action.GetOrCreate<FollowUpOwnerPoolMetadata>();

        switch (pool.Source)
        {
            case FollowUpOwnerPoolSource.Team when string.IsNullOrWhiteSpace(pool.TeamId):
                context.Result.Fail(new ValidationResult(S["Choose the team whose members get the follow-up."], ["PoolTeamId"]));
                break;

            case FollowUpOwnerPoolSource.Roles when !(pool.RoleNames ?? []).Any(role => !string.IsNullOrWhiteSpace(role)):
                context.Result.Fail(new ValidationResult(S["Choose at least one role."], ["PoolRoleNames"]));
                break;

            case FollowUpOwnerPoolSource.Users when !(pool.UserIds ?? []).Any(user => !string.IsNullOrWhiteSpace(user)):
                context.Result.Fail(new ValidationResult(S["Choose at least one user."], ["PoolUserIds"]));
                break;
        }
    }

    private async Task ValidateOwnerAsync(
        ValidatingContext<SubjectAction> context,
        SubjectActionOwnerAssignmentType assignmentType,
        string normalizedUserName)
    {
        if (assignmentType != SubjectActionOwnerAssignmentType.SpecificOwner)
        {
            return;
        }

        var owner = normalizedUserName?.Trim();

        if (string.IsNullOrWhiteSpace(owner))
        {
            context.Result.Fail(new ValidationResult(S["Choose the user who owns the follow-up."], [nameof(NewActivityActionMetadata.NormalizedUserName)]));

            return;
        }

        if (await _session.QueryIndex<UserIndex>(x => x.NormalizedUserName == owner).FirstOrDefaultAsync() is null)
        {
            context.Result.Fail(new ValidationResult(S["The selected user does not exist."], [nameof(NewActivityActionMetadata.NormalizedUserName)]));
        }
    }
}
