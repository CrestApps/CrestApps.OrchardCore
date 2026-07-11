using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class TryAgainSubjectActionDisplayDriver : DisplayDriver<SubjectAction>
{
    private readonly ISession _session;
    private readonly IDisplayNameProvider _displayNameProvider;

    internal readonly IStringLocalizer S;

    public TryAgainSubjectActionDisplayDriver(
        ISession session,
        IDisplayNameProvider displayNameProvider,
        IStringLocalizer<TryAgainSubjectActionDisplayDriver> stringLocalizer)
    {
        _session = session;
        _displayNameProvider = displayNameProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(SubjectAction action, BuildEditorContext context)
    {
        if (!string.Equals(action.Source, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Initialize<TryAgainSubjectActionViewModel>("TryAgainSubjectActionFields_Edit", async model =>
        {
            if (action.TryGet<TryAgainActionMetadata>(out var metadata))
            {
                model.MaxAttempt = metadata.MaxAttempt;
                model.UrgencyLevel = metadata.UrgencyLevel;
                model.AssignmentType = SubjectActionOwnerAssignmentTypeResolver.Resolve(metadata.AssignmentType, metadata.NormalizedUserName);
                model.NormalizedUserName = metadata.NormalizedUserName;
                model.DefaultScheduleHours = metadata.DefaultScheduleHours;

                if (!string.IsNullOrWhiteSpace(metadata.NormalizedUserName))
                {
                    var selectedUser = await _session.Query<User, UserIndex>(x => x.NormalizedUserName == metadata.NormalizedUserName).FirstOrDefaultAsync();

                    if (selectedUser != null)
                    {
                        model.SelectedUsers =
                        [
                            new(await _displayNameProvider.GetAsync(selectedUser), metadata.NormalizedUserName),
                        ];
                    }
                    else
                    {
                        model.SelectedUsers =
                        [
                            new(metadata.NormalizedUserName, metadata.NormalizedUserName),
                        ];
                    }
                }
            }

            model.SelectedUsers ??= [];
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(SubjectAction action, UpdateEditorContext context)
    {
        if (!string.Equals(action.Source, OmnichannelConstants.ActionTypes.TryAgain, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var model = new TryAgainSubjectActionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var normalizedUserName = model.NormalizedUserName?.Trim();

        if (model.AssignmentType == SubjectActionOwnerAssignmentType.SpecificOwner)
        {
            if (string.IsNullOrWhiteSpace(normalizedUserName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.NormalizedUserName), S["A user is required when the assignment type is Specific owner."]);
            }
            else if (await _session.Query<User, UserIndex>(x => x.NormalizedUserName == normalizedUserName).FirstOrDefaultAsync() is null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.NormalizedUserName), S["The selected user does not exist."]);
            }
        }
        else
        {
            normalizedUserName = null;
        }

        action.Put(new TryAgainActionMetadata
        {
            MaxAttempt = model.MaxAttempt,
            UrgencyLevel = model.UrgencyLevel,
            AssignmentType = model.AssignmentType,
            NormalizedUserName = normalizedUserName,
            DefaultScheduleHours = model.DefaultScheduleHours,
        });

        return Edit(action, context);
    }
}
