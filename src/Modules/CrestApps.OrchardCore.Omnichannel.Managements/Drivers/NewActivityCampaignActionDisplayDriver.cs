using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class NewActivityCampaignActionDisplayDriver : DisplayDriver<CampaignAction>
{
    private readonly ICatalog<OmnichannelCampaign> _campaignCatalog;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ISession _session;
    private readonly IDisplayNameProvider _displayNameProvider;

    public NewActivityCampaignActionDisplayDriver(
        ICatalog<OmnichannelCampaign> campaignCatalog,
        IContentDefinitionManager contentDefinitionManager,
        ISession session,
        IDisplayNameProvider displayNameProvider)
    {
        _campaignCatalog = campaignCatalog;
        _contentDefinitionManager = contentDefinitionManager;
        _session = session;
        _displayNameProvider = displayNameProvider;
    }

    public override IDisplayResult Edit(CampaignAction action, BuildEditorContext context)
    {
        if (!string.Equals(action.Source, OmnichannelConstants.ActionTypes.NewActivity, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Initialize<NewActivityCampaignActionViewModel>("NewActivityCampaignActionFields_Edit", async model =>
        {
            if (action.TryGet<NewActivityActionMetadata>(out var metadata))
            {
                model.CampaignId = metadata.CampaignId;
                model.SubjectContentType = metadata.SubjectContentType;
                model.UrgencyLevel = metadata.UrgencyLevel;
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

            var campaigns = await _campaignCatalog.GetAllAsync();

            model.Campaigns = campaigns
                .Select(c => new SelectListItem
                {
                    Text = c.DisplayText,
                    Value = c.ItemId,
                    Selected = string.Equals(c.ItemId, metadata?.CampaignId, StringComparison.OrdinalIgnoreCase),
                })
                .OrderBy(x => x.Text);

            var subjectTypes = await _contentDefinitionManager.ListTypeDefinitionsAsync();

            model.SubjectContentTypes = subjectTypes
                .Where(t => t.StereotypeEquals(OmnichannelConstants.Sterotypes.OmnichannelSubject))
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

    public override async Task<IDisplayResult> UpdateAsync(CampaignAction action, UpdateEditorContext context)
    {
        if (!string.Equals(action.Source, OmnichannelConstants.ActionTypes.NewActivity, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var model = new NewActivityCampaignActionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        action.Put(new NewActivityActionMetadata
        {
            CampaignId = model.CampaignId,
            SubjectContentType = model.SubjectContentType,
            UrgencyLevel = model.UrgencyLevel,
            NormalizedUserName = model.NormalizedUserName?.Trim(),
            DefaultScheduleHours = model.DefaultScheduleHours,
        });

        return Edit(action, context);
    }
}
