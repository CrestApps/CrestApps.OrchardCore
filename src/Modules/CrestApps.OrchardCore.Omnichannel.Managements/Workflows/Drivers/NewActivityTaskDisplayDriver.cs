using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Tasks;
using CrestApps.OrchardCore.Omnichannel.Managements.Workflows.ViewModels;
using CrestApps.OrchardCore.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Drivers;

internal sealed class NewActivityTaskDisplayDriver : ActivityDisplayDriver<NewActivityTask, NewActivityTaskViewModel>
{
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly ICatalog<OmnichannelCampaign> _campaignCatalog;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    internal readonly IStringLocalizer S;

    public NewActivityTaskDisplayDriver(
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        ICatalog<OmnichannelCampaign> campaignCatalog,
        IContentDefinitionManager contentDefinitionManager,
        IStringLocalizer<TryAgainActivityTaskDisplayDriver> stringLocalizer)
    {
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
        _campaignCatalog = campaignCatalog;
        _contentDefinitionManager = contentDefinitionManager;
        S = stringLocalizer;
    }

    protected override async ValueTask EditActivityAsync(NewActivityTask activity, NewActivityTaskViewModel model)
    {
        model.UrgencyLevel = activity.UrgencyLevel;
        model.CampaignId = activity.CampaignId;
        model.SubjectContentType = activity.SubjectContentType;

        model.NormalizedUserName = activity.NormalizedUserName;
        model.DefaultScheduleHours = activity.DefaultScheduleHours;
        model.UrgencyLevels =
        [
            new(S["Normal"], nameof(ActivityUrgencyLevel.Normal)),
            new(S["Very low"], nameof(ActivityUrgencyLevel.VeryLow)),
            new(S["Low"], nameof(ActivityUrgencyLevel.Low)),
            new(S["Medium"], nameof(ActivityUrgencyLevel.Medium)),
            new(S["High"], nameof(ActivityUrgencyLevel.High)),
            new(S["Very high"], nameof(ActivityUrgencyLevel.VeryHigh)),
        ];

        model.Campaigns = (await _campaignCatalog.GetAllAsync()).Select(x => new SelectListItem(x.DisplayText, x.ItemId)).OrderBy(x => x.Text);

        var subjectContentTypes = new List<SelectListItem>();

        foreach (var contentType in await _contentDefinitionManager.ListTypeDefinitionsAsync())
        {
            if (contentType.StereotypeEquals(OmnichannelConstants.Sterotypes.OmnichannelSubject))
            {
                subjectContentTypes.Add(new SelectListItem(contentType.DisplayName, contentType.Name));
            }
        }

        model.SubjectContentTypes = subjectContentTypes.OrderBy(x => x.Text);

        var users = await _userManager.GetUsersInRoleAsync(OmnichannelConstants.AgentRole);

        var userItems = new List<SelectListItem>();

        foreach (var user in users)
        {
            var displayName = await _displayNameProvider.GetAsync(user);

            var normalizedUserName = user is User su ? su.NormalizedUserName : _userManager.NormalizeName(user.UserName);

            userItems.Add(new SelectListItem
            {
                Text = displayName,
                Value = normalizedUserName,
            });
        }

        model.Users = userItems.OrderBy(x => x.Text);
    }

    public override async Task<IDisplayResult> UpdateAsync(NewActivityTask activity, UpdateEditorContext context)
    {
        var model = new NewActivityTaskViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.DefaultScheduleHours.HasValue && model.DefaultScheduleHours < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DefaultScheduleHours), S["Default schedule field must have min of {0}", 1]);
        }

        activity.CampaignId = model.CampaignId;
        activity.SubjectContentType = model.SubjectContentType;
        activity.UrgencyLevel = model.UrgencyLevel;
        activity.NormalizedUserName = model.NormalizedUserName?.Trim();
        activity.DefaultScheduleHours = model.DefaultScheduleHours;

        return await EditAsync(activity, context);
    }
}
