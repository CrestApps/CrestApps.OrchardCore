using System.Security.Claims;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelActivityDisplayDriver : DisplayDriver<OmnichannelActivity>
{
    private readonly ICatalog<OmnichannelDisposition> _dispositionsCatalog;
    private readonly ICatalog<OmnichannelCampaign> _campaignsCatalog;
    private readonly ICatalog<OmnichannelChannelEndpoint> _channelEndpointsCatalog;
    private readonly INamedCatalog<AIProfile> _aiProfileCatalog;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<IUser> _userManager;

    internal readonly IStringLocalizer S;

    public OmnichannelActivityDisplayDriver(
        ICatalog<OmnichannelDisposition> dispositionsCatalog,
        ICatalog<OmnichannelCampaign> campaignsCatalog,
        ICatalog<OmnichannelChannelEndpoint> channelEndpointsCatalog,
        INamedCatalog<AIProfile> aiProfileCatalog,
        IContentDefinitionManager contentDefinitionManager,
        IDisplayNameProvider displayNameProvider,
        IClock clock,
        ILocalClock localClock,
        UserManager<IUser> userManager,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<OmnichannelActivityDisplayDriver> stringLocalizer)
    {
        _dispositionsCatalog = dispositionsCatalog;
        _campaignsCatalog = campaignsCatalog;
        _channelEndpointsCatalog = channelEndpointsCatalog;
        _aiProfileCatalog = aiProfileCatalog;
        _contentDefinitionManager = contentDefinitionManager;
        _displayNameProvider = displayNameProvider;
        _clock = clock;
        _localClock = localClock;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(OmnichannelActivity activity, BuildEditorContext context)
    {
        var fields = Initialize<EditOmnichannelActivity>("OmnichannelActivityFields_Edit", async model =>
        {
            model.Channel = activity.Channel;
            model.CampaignId = activity.CampaignId;
            model.ScheduleAt = context.IsNew || activity.ScheduledAt == DateTime.MinValue
                ? (await _localClock.GetLocalNowAsync()).DateTime
                : activity.ScheduledAt;
            model.InteractionType = activity.InteractionType;
            model.AIProfileName = activity.AIProfileName;
            model.SubjectContentType = activity.SubjectContentType;
            model.AIProfileName = activity.AIProfileName;
            model.UserId = activity.AssignedToId ?? _httpContextAccessor.HttpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            model.Instructions = activity.Instructions;
            model.UrgencyLevel = activity.UrgencyLevel;

            model.Campaigns = (await _campaignsCatalog.GetAllAsync()).Select(x => new SelectListItem(x.DisplayText, x.Id)).OrderBy(x => x.Text);

            var subjectContentTypes = new List<SelectListItem>();
            var contactContentTypes = new List<SelectListItem>();

            foreach (var contentType in await _contentDefinitionManager.ListTypeDefinitionsAsync())
            {
                if (!contentType.TryGetStereotype(out var stereotype))
                {
                    continue;
                }

                if (contentType.StereotypeEquals(OmnichannelConstants.Sterotypes.OmnichannelSubject))
                {
                    subjectContentTypes.Add(new SelectListItem(contentType.DisplayName, contentType.Name));
                }

                if (contentType.Parts.Any(x => x.Name == OmnichannelConstants.ContentParts.OmnichannelContact))
                {
                    contactContentTypes.Add(new SelectListItem(contentType.DisplayName, contentType.Name));
                }
            }

            var users = await _userManager.GetUsersInRoleAsync(OmnichannelConstants.AgentRole);

            var usersListItems = new List<SelectListItem>();

            foreach (var user in users)
            {
                var displayName = await _displayNameProvider.GetAsync(user);

                usersListItems.Add(new SelectListItem(displayName, _userManager.NormalizeName(user.UserName)));
            }
            model.AIProfiles = (await _aiProfileCatalog.GetAllAsync()).Select(x => new SelectListItem(x.DisplayText ?? x.Name, x.Name)).OrderBy(x => x.Text);
            model.ChannelEndpoints = (await _channelEndpointsCatalog.GetAllAsync()).Select(x => new SelectListItem(x.DisplayText, x.Id)).OrderBy(x => x.Text);
            model.Channels =
            [
                new(S["Phone"], OmnichannelConstants.Channels.Phone),
                new(S["SMS"], OmnichannelConstants.Channels.Sms),
                new(S["Email"], OmnichannelConstants.Channels.Email),
            ];

            model.InteractionTypes =
            [
                new(S["Manual"], nameof(ActivityInteractionType.Manual)),
                new(S["Automated"], nameof(ActivityInteractionType.Automated)),
            ];

            model.UrgencyLevels =
            [
                new(S["Normal"], nameof(ActivityUrgencyLevel.Normal)),
                new(S["Very low"], nameof(ActivityUrgencyLevel.VeryLow)),
                new(S["Low"], nameof(ActivityUrgencyLevel.Low)),
                new(S["Medium"], nameof(ActivityUrgencyLevel.Medium)),
                new(S["High"], nameof(ActivityUrgencyLevel.High)),
                new(S["Very high"], nameof(ActivityUrgencyLevel.VeryHigh)),
            ];
            model.SubjectContentTypes = subjectContentTypes.OrderBy(x => x.Text);
            model.ContactContentTypes = contactContentTypes.OrderBy(x => x.Text);
            model.Users = usersListItems.OrderBy(x => x.Text);

        }).Location("Content:5");

        var process = Initialize<OmnichannelActivityViewModel>("OmnichannelActivityProcess_Edit", async model =>
        {
            var campaign = await _campaignsCatalog.FindByIdAsync(activity.CampaignId);

            var campaignDispositionIds = campaign?.DispositionIds ?? [];

            if (!string.IsNullOrEmpty(activity.DispositionId) && !campaignDispositionIds.Contains(activity.DispositionId))
            {
                campaignDispositionIds.Add(activity.DispositionId);
            }

            model.CampaignTitle = campaign?.DisplayText;
            model.Channel = activity.Channel;
            model.InteractionType = activity.InteractionType.ToString();
            model.Instructions = activity.Instructions;
            model.Dispositions = await _dispositionsCatalog.GetAsync(campaignDispositionIds);
            model.Notes = activity.Notes;
            model.DispositionId = activity.DispositionId;
        }).Location("Content:5")
        .OnGroup("Process");

        return Combine(fields, process);
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelActivity activity, UpdateEditorContext context)
    {
        if (context.GroupId == "Process")
        {
            // The following fields are for processing a task.
            var processModel = new OmnichannelActivityViewModel();

            await context.Updater.TryUpdateModelAsync(processModel, Prefix);

            if (string.IsNullOrEmpty(processModel.DispositionId))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(processModel.DispositionId), S["The Disposition field is required."]);
            }
            else
            {
                var campaign = await _campaignsCatalog.FindByIdAsync(activity.CampaignId);

                var campaignDispositionIds = campaign?.DispositionIds ?? [];

                if (!string.IsNullOrEmpty(activity.DispositionId))
                {
                    campaignDispositionIds.Add(activity.DispositionId);
                }

                var dispositions = await _dispositionsCatalog.GetAsync(campaignDispositionIds);

                var disposition = dispositions.FirstOrDefault(d => d.Id == processModel.DispositionId);

                if (disposition == null)
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(processModel.DispositionId), S["The selected Disposition is invalid."]);
                }
                else if (disposition.CaptureDate && !processModel.ScheduleDate.HasValue)
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(processModel.ScheduleDate), S["The Schedule Date field is required."]);
                }
            }

            activity.DispositionId = processModel.DispositionId;
            activity.Notes = processModel.Notes;

            activity.Put(new DispositionMetadata
            {
                ScheduledDate = processModel.ScheduleDate,
            });

            return Edit(activity, context);
        }

        var model = new EditOmnichannelActivity();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.Channel))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Channel), S["Channel is required."]);
        }

        if (string.IsNullOrEmpty(model.SubjectContentType))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SubjectContentType), S["Subject is required."]);
        }

        if (string.IsNullOrEmpty(model.CampaignId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CampaignId), S["Campaign is required."]);
        }

        if (string.IsNullOrEmpty(model.UserId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.UserId), S["Contact is required."]);
        }

        if (!model.ScheduleAt.HasValue)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ScheduleAt), S["Schedule at field is required."]);
        }

        if (model.InteractionType == ActivityInteractionType.Automated)
        {
            if (string.IsNullOrEmpty(model.ChannelEndpoint))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ChannelEndpoint), S["Channel endpoint at field is required."]);
            }

            if (string.IsNullOrEmpty(model.AIProfileName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.AIProfileName), S["AI Profile is required for automated activities."]);
            }
            else
            {
                var aiProfile = await _aiProfileCatalog.FindByNameAsync(model.AIProfileName);
                if (aiProfile == null)
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.AIProfileName), S["The selected AI Profile is invalid."]);
                }
            }
        }

        activity.Channel = model.Channel;
        activity.CampaignId = model.CampaignId;
        activity.InteractionType = model.InteractionType;
        activity.SubjectContentType = model.SubjectContentType;
        activity.ContactContentType = activity.ContactContentType;
        activity.ChannelEndpoint = model.ChannelEndpoint;
        activity.AIProfileName = model.AIProfileName;
        activity.Instructions = model.Instructions?.Trim();
        activity.UrgencyLevel = model.UrgencyLevel;

        if (activity.AssignedToId != model.UserId ||
            string.IsNullOrEmpty(activity.AssignedToId) ||
            !activity.AssignedToUtc.HasValue)
        {
            activity.AssignedToUtc = _clock.UtcNow;
            activity.AssignedToUsername = (await _userManager.FindByIdAsync(model.UserId))?.UserName;
            activity.AssignedToId = model.UserId;
        }

        if (model.ScheduleAt.HasValue)
        {
            activity.ScheduledAt = await _localClock.ConvertToUtcAsync(model.ScheduleAt.Value);
        }

        return Edit(activity, context);
    }
}
