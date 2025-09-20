using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
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

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelActivityBatchDisplayDriver : DisplayDriver<OmnichannelActivityBatch>
{
    private readonly ICatalog<OmnichannelCampaign> _catalog;
    private readonly ICatalog<OmnichannelChannelEndpoint> _channelEndpointsCatalog;
    private readonly INamedCatalog<AIProfile> _aiProfileCatalog;
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    internal readonly IStringLocalizer S;

    public OmnichannelActivityBatchDisplayDriver(
        ICatalog<OmnichannelCampaign> catalog,
        ICatalog<OmnichannelChannelEndpoint> channelEndpointsCatalog,
        INamedCatalog<AIProfile> aiProfileCatalog,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        IContentDefinitionManager contentDefinitionManager,
        IStringLocalizer<OmnichannelActivityBatchDisplayDriver> stringLocalizer)
    {
        _catalog = catalog;
        _channelEndpointsCatalog = channelEndpointsCatalog;
        _aiProfileCatalog = aiProfileCatalog;
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
        _contentDefinitionManager = contentDefinitionManager;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(OmnichannelActivityBatch batch, BuildDisplayContext context)
    {
        var results = new List<IDisplayResult>()
        {
            View("OmnichannelActivityBatch_Fields_SummaryAdmin", batch).Location("Content:1"),
            View("OmnichannelActivityBatch_Buttons_SummaryAdmin", batch).Location("Actions:5"),
            View("OmnichannelActivityBatch_DefaultMeta_SummaryAdmin", batch).Location("Meta:5"),
        };

        if (batch.Status == OmnichannelActivityBatchStatus.New)
        {
            results.Add(View("OmnichannelActivityBatch_ActionsMenuItems_SummaryAdmin", batch).Location("ActionsMenu:10"));
        }

        return CombineAsync(results);
    }

    public override IDisplayResult Edit(OmnichannelActivityBatch batch, BuildEditorContext context)
    {
        return Initialize<OmnichannelActivityBatchViewModel>("OmnichannelActivityBatchFields_Edit", async model =>
        {
            model.DisplayText = batch.DisplayText;
            model.Channel = batch.Channel;
            model.CampaignId = batch.CampaignId;
            model.ScheduleAt = batch.ScheduleAt;
            model.InteractionType = batch.InteractionType;
            model.AIProfileName = batch.AIProfileName;
            model.SubjectContentType = batch.SubjectContentType;
            model.ContactContentType = batch.ContactContentType;
            model.ChannelEndpoint = batch.ChannelEndpoint;
            model.AIProfileName = batch.AIProfileName;
            model.UserIds = batch.NormalizedUserNames;
            model.IncludeDoNoCalls = batch.IncludeDoNoCalls;
            model.IncludeDoNoSms = batch.IncludeDoNoSms;
            model.IncludeDoNoEmail = batch.IncludeDoNoEmail;
            model.PreventDuplicates = context.IsNew || batch.PreventDuplicates;
            model.Instructions = batch.Instructions;
            model.UrgencyLevel = batch.UrgencyLevel;

            model.Campaigns = (await _catalog.GetAllAsync()).Select(x => new SelectListItem(x.DisplayText, x.Id)).OrderBy(x => x.Text);

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
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelActivityBatch batch, UpdateEditorContext context)
    {
        var model = new OmnichannelActivityBatchViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["Title is required."]);
        }

        if (string.IsNullOrEmpty(model.Channel))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Channel), S["Channel is required."]);
        }

        if (string.IsNullOrEmpty(model.SubjectContentType))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SubjectContentType), S["Subject is required."]);
        }

        if (string.IsNullOrEmpty(model.ContactContentType))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ContactContentType), S["Contact is required."]);
        }

        if (model.UserIds is null || model.UserIds.Length == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.UserIds), S["At least one user is required."]);
        }

        if (model.ScheduleAt is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ScheduleAt), S["Schedule at field is required."]);
        }

        batch.DisplayText = model.DisplayText?.Trim();
        batch.Channel = model.Channel;
        batch.CampaignId = model.CampaignId;
        batch.InteractionType = model.InteractionType;
        batch.SubjectContentType = model.SubjectContentType;
        batch.ContactContentType = model.ContactContentType;
        batch.ChannelEndpoint = model.ChannelEndpoint;
        batch.AIProfileName = model.AIProfileName;
        batch.Instructions = model.Instructions?.Trim();
        batch.UrgencyLevel = model.UrgencyLevel;
        batch.NormalizedUserNames = model.UserIds ?? [];
        batch.IncludeDoNoCalls = model.IncludeDoNoCalls;
        batch.IncludeDoNoSms = model.IncludeDoNoSms;
        batch.IncludeDoNoEmail = model.IncludeDoNoEmail;
        batch.PreventDuplicates = model.PreventDuplicates;
        if (model.ScheduleAt.HasValue)
        {
            batch.ScheduleAt = model.ScheduleAt.Value;
        }

        return Edit(batch, context);
    }
}
