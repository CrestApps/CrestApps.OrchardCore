using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class NewActivitySubjectActionDisplayDriver : DisplayDriver<SubjectAction>
{
    private readonly ISession _session;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;

    internal readonly IStringLocalizer S;

    public NewActivitySubjectActionDisplayDriver(
        ISession session,
        IDisplayNameProvider displayNameProvider,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IStringLocalizer<NewActivitySubjectActionDisplayDriver> stringLocalizer)
    {
        _session = session;
        _displayNameProvider = displayNameProvider;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(SubjectAction action, BuildEditorContext context)
    {
        if (!string.Equals(action.Source, OmnichannelConstants.ActionTypes.NewActivity, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Initialize<NewActivitySubjectActionViewModel>("NewActivitySubjectActionFields_Edit", async model =>
        {
            if (action.TryGet<NewActivityActionMetadata>(out var metadata))
            {
                model.SubjectContentType = metadata.SubjectContentType;
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
                            new SelectListItem(await _displayNameProvider.GetAsync(selectedUser), metadata.NormalizedUserName),
                        ];
                    }
                    else
                    {
                        model.SelectedUsers =
                        [
                            new SelectListItem(metadata.NormalizedUserName, metadata.NormalizedUserName),
                        ];
                    }
                }
            }

            var subjectTypes = await _subjectFlowSettingsService.GetConfiguredSubjectTypesAsync();

            model.SubjectContentTypes = subjectTypes
                .Select(t => new SelectListItem
                {
                    Text = t.DisplayName,
                    Value = t.Name,
                    Selected = string.Equals(t.Name, metadata?.SubjectContentType, StringComparison.OrdinalIgnoreCase),
                })
                .OrderBy(x => x.Text);

            model.SelectedUsers ??= [];
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(SubjectAction action, UpdateEditorContext context)
    {
        if (!string.Equals(action.Source, OmnichannelConstants.ActionTypes.NewActivity, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var model = new NewActivitySubjectActionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var normalizedUserName = model.NormalizedUserName?.Trim();

        if (!string.IsNullOrWhiteSpace(model.SubjectContentType) &&
            await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(model.SubjectContentType) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SubjectContentType), S["The selected subject must be configured under Subject Flows before it can be used by a New Activity action."]);
        }

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

        action.Put(new NewActivityActionMetadata
        {
            SubjectContentType = model.SubjectContentType,
            UrgencyLevel = model.UrgencyLevel,
            AssignmentType = model.AssignmentType,
            NormalizedUserName = normalizedUserName,
            DefaultScheduleHours = model.DefaultScheduleHours,
        });

        return Edit(action, context);
    }
}
