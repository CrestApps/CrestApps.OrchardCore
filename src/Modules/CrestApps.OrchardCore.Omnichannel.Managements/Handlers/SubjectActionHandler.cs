using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Declares the rules a subject action must satisfy before it can be stored, whoever is writing it.
/// </summary>
internal sealed class SubjectActionHandler : CatalogEntryHandlerBase<SubjectAction>
{
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly ISession _session;

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
        IStringLocalizer<SubjectActionHandler> stringLocalizer)
    {
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _session = session;
        S = stringLocalizer;
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
        }
        else if (string.Equals(action.Source, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.OrdinalIgnoreCase))
        {
            var metadata = action.GetOrCreate<TryAgainActionMetadata>();

            await ValidateOwnerAsync(context, metadata.AssignmentType, metadata.NormalizedUserName);
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
            context.Result.Fail(new ValidationResult(S["A user is required when the assignment type is Specific owner."], [nameof(NewActivityActionMetadata.NormalizedUserName)]));

            return;
        }

        if (await _session.Query<User, UserIndex>(x => x.NormalizedUserName == owner).FirstOrDefaultAsync() is null)
        {
            context.Result.Fail(new ValidationResult(S["The selected user does not exist."], [nameof(NewActivityActionMetadata.NormalizedUserName)]));
        }
    }
}
