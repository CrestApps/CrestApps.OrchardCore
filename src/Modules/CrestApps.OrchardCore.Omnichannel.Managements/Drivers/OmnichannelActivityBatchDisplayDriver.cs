using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelActivityBatchDisplayDriver : DisplayDriver<OmnichannelActivityBatch>
{
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;
    private readonly ISession _session;
    private readonly INamedCatalog<OmnichannelDisposition> _dispositionsCatalog;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityBatchDisplayDriver"/> class.
    /// </summary>
    /// <param name="displayNameProvider">The display name provider.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="session">The YesSql session.</param>
    /// <param name="dispositionsCatalog">The dispositions catalog.</param>
    /// <param name="subjectFlowSettingsService">The subject flow settings service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelActivityBatchDisplayDriver(
        IDisplayNameProvider displayNameProvider,
        IContentDefinitionManager contentDefinitionManager,
        IClock clock,
        ILocalClock localClock,
        ISession session,
        INamedCatalog<OmnichannelDisposition> dispositionsCatalog,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IStringLocalizer<OmnichannelActivityBatchDisplayDriver> stringLocalizer)
    {
        _displayNameProvider = displayNameProvider;
        _contentDefinitionManager = contentDefinitionManager;
        _clock = clock;
        _localClock = localClock;
        _session = session;
        _dispositionsCatalog = dispositionsCatalog;
        _subjectFlowSettingsService = subjectFlowSettingsService;
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
            model.ScheduleAt = context.IsNew ? (await _localClock.GetLocalNowAsync()).DateTime : batch.ScheduleAt;
            model.SubjectContentType = batch.SubjectContentType;
            model.ContactContentType = batch.ContactContentType;
            model.UserIds = batch.UserIds;
            model.IncludeDoNoCalls = batch.IncludeDoNoCalls;
            model.IncludeDoNoSms = batch.IncludeDoNoSms;
            model.IncludeDoNoEmail = batch.IncludeDoNoEmail;
            model.PreventDuplicates = context.IsNew || batch.PreventDuplicates;
            model.Instructions = batch.Instructions;
            model.UrgencyLevel = batch.UrgencyLevel;
            model.LeadCreatedFrom = batch.LeadCreatedFrom;
            model.LeadCreatedTo = batch.LeadCreatedTo;
            model.OnlyPublishedLeads = context.IsNew || batch.OnlyPublishedLeads;
            model.Limit = batch.Limit;
            model.PhoneNumber = batch.PhoneNumber;
            model.PhoneNumberMatchType = batch.PhoneNumberMatchType;
            model.TimeZoneIds = batch.TimeZoneIds ?? [];
            model.LastActivitySubjectContentType = batch.LastActivitySubjectContentType;
            model.LastActivityDispositionId = batch.LastActivityDispositionId;

            var subjectContentTypes = new List<SelectListItem>();
            var contactContentTypes = new List<SelectListItem>();

            foreach (var contentType in await _subjectFlowSettingsService.GetConfiguredSubjectTypesAsync())
            {
                subjectContentTypes.Add(new SelectListItem(contentType.DisplayName, contentType.Name));
            }

            foreach (var contentType in await _contentDefinitionManager.ListTypeDefinitionsAsync())
            {
                if (contentType.Parts.Any(x => x.Name == OmnichannelConstants.ContentParts.OmnichannelContact))
                {
                    contactContentTypes.Add(new SelectListItem(contentType.DisplayName, contentType.Name));
                }
            }

            if (batch.UserIds is { Length: > 0 })
            {
                var users = (await _session.Query<User, UserIndex>(x => x.UserId.IsIn(batch.UserIds)).ListAsync())
                    .OrderBy(user => Array.FindIndex(batch.UserIds, itemId => string.Equals(itemId, user.UserId, StringComparison.OrdinalIgnoreCase)));

                var selectedUsers = new List<SelectListItem>();

                foreach (var user in users)
                {
                    var displayName = await _displayNameProvider.GetAsync(user);

                    selectedUsers.Add(new SelectListItem(displayName, user.UserId));
                }

                model.SelectedUsers = selectedUsers;
            }

            model.UrgencyLevels =
            [
                new(S["Normal"], nameof(ActivityUrgencyLevel.Normal)),
                new(S["Very low"], nameof(ActivityUrgencyLevel.VeryLow)),
                new(S["Low"], nameof(ActivityUrgencyLevel.Low)),
                new(S["Medium"], nameof(ActivityUrgencyLevel.Medium)),
                new(S["High"], nameof(ActivityUrgencyLevel.High)),
                new(S["Very high"], nameof(ActivityUrgencyLevel.VeryHigh)),
            ];

            model.PhoneNumberMatchTypes =
            [
                new(S["Begins with"], nameof(PhoneNumberMatchType.BeginsWith)),
                new(S["Ends with"], nameof(PhoneNumberMatchType.EndsWith)),
                new(S["Exact match"], nameof(PhoneNumberMatchType.Exact)),
            ];

            var timeZones = new List<SelectListItem>();

            foreach (var timeZone in _clock.GetTimeZones())
            {
                timeZones.Add(new SelectListItem(timeZone.TimeZoneId, timeZone.TimeZoneId));
            }

            model.TimeZones = timeZones.OrderBy(x => x.Text);

            var allDispositions = await _dispositionsCatalog.GetAllAsync();
            var dispositionItems = new List<SelectListItem>
            {
                new(S["Any disposition"], ""),
            };

            foreach (var disposition in allDispositions.OrderBy(d => d.Name))
            {
                dispositionItems.Add(new SelectListItem(disposition.Name, disposition.ItemId));
            }

            model.Dispositions = dispositionItems;

            model.SubjectContentTypes = subjectContentTypes.OrderBy(x => x.Text);
            model.ContactContentTypes = contactContentTypes.OrderBy(x => x.Text);
            model.SelectedUsers ??= [];
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

        if (string.IsNullOrEmpty(model.SubjectContentType))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SubjectContentType), S["Subject is required."]);
        }
        else if (await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(model.SubjectContentType) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SubjectContentType), S["The selected subject must be configured under Subject Flows before activity batches can load activities."]);
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

        if (!string.IsNullOrEmpty(model.PhoneNumber) && !model.PhoneNumber.TrimStart().StartsWith('+'))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PhoneNumber), S["Phone number must be in E.164 format (e.g., +17025551234 for US/Canada)."]);
        }

        batch.DisplayText = model.DisplayText?.Trim();
        batch.SubjectContentType = model.SubjectContentType;
        batch.ContactContentType = model.ContactContentType;

        batch.Instructions = model.Instructions?.Trim();
        batch.UrgencyLevel = model.UrgencyLevel;
        batch.UserIds = model.UserIds ?? [];
        batch.IncludeDoNoCalls = model.IncludeDoNoCalls;
        batch.IncludeDoNoSms = model.IncludeDoNoSms;
        batch.IncludeDoNoEmail = model.IncludeDoNoEmail;
        batch.PreventDuplicates = model.PreventDuplicates;
        batch.LeadCreatedFrom = model.LeadCreatedFrom;
        batch.LeadCreatedTo = model.LeadCreatedTo;
        batch.OnlyPublishedLeads = model.OnlyPublishedLeads;
        batch.Limit = model.Limit;
        batch.PhoneNumber = model.PhoneNumber?.Trim();
        batch.PhoneNumberMatchType = model.PhoneNumberMatchType;
        batch.TimeZoneIds = model.TimeZoneIds;
        batch.LastActivitySubjectContentType = model.LastActivitySubjectContentType;
        batch.LastActivityDispositionId = model.LastActivityDispositionId;

        if (model.ScheduleAt.HasValue)
        {
            batch.ScheduleAt = model.ScheduleAt.Value;
        }

        return Edit(batch, context);
    }
}
