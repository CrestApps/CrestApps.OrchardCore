using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.DisplayManagement.Zones;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelActivityBatchDisplayDriver : DisplayDriver<OmnichannelActivityBatch>
{
    /// <summary>
    /// The editor zone shown inside the record filters card. Filters that apply to one kind of record, such as the
    /// lead filters, are placed here so they sit beside the record type that turns them on.
    /// </summary>
    internal const string RecordFiltersZone = "RecordFilters";

    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly OmnichannelContentTypeProvider _contentTypeProvider;
    private readonly ITimeZoneSelectListProvider _timeZoneSelectListProvider;
    private readonly ILocalClock _localClock;
    private readonly ISession _session;
    private readonly INamedCatalog<OmnichannelDisposition> _dispositionsCatalog;
    private readonly ICatalog<OmnichannelCampaign> _campaignCatalog;
    private readonly ICatalog<Cadence> _cadenceCatalog;
    private readonly ICatalog<OmnichannelChannelEndpoint> _channelEndpointsCatalog;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly BulkActivityAdminFormOptionsProvider _optionsProvider;
    private readonly ActivityBatchSourceOptions _activityBatchSourceOptions;
    private readonly ActivityChannelOptions _activityChannelOptions;
    private readonly IAIProfileManager _aiProfileManager;
    private readonly IBusinessHoursGate _businessHoursGate;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityBatchDisplayDriver"/> class.
    /// </summary>
    /// <param name="displayNameProvider">The display name provider.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="contentTypeProvider">The content type provider.</param>
    /// <param name="timeZoneSelectListProvider">The time zone select list provider.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="session">The YesSql session.</param>
    /// <param name="dispositionsCatalog">The dispositions catalog.</param>
    /// <param name="campaignCatalog">The campaign catalog.</param>
    /// <param name="channelEndpointsCatalog">The channel endpoints catalog.</param>
    /// <param name="subjectFlowSettingsService">The subject flow settings service.</param>
    /// <param name="optionsProvider">The bulk activity options provider.</param>
    /// <param name="activityBatchSourceOptions">The configured activity batch sources.</param>
    /// <param name="activityChannelOptions">The channels activities can be loaded on.</param>
    /// <param name="aiProfileManagers">The optional AI profile managers, present only when the AI feature is enabled.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelActivityBatchDisplayDriver(
        IDisplayNameProvider displayNameProvider,
        IContentDefinitionManager contentDefinitionManager,
        OmnichannelContentTypeProvider contentTypeProvider,
        ITimeZoneSelectListProvider timeZoneSelectListProvider,
        ILocalClock localClock,
        ISession session,
        INamedCatalog<OmnichannelDisposition> dispositionsCatalog,
        ICatalog<OmnichannelCampaign> campaignCatalog,
        ICatalog<Cadence> cadenceCatalog,
        ICatalog<OmnichannelChannelEndpoint> channelEndpointsCatalog,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        BulkActivityAdminFormOptionsProvider optionsProvider,
        IOptions<ActivityBatchSourceOptions> activityBatchSourceOptions,
        IOptions<ActivityChannelOptions> activityChannelOptions,
        IEnumerable<IAIProfileManager> aiProfileManagers,
        IBusinessHoursGate businessHoursGate,
        IStringLocalizer<OmnichannelActivityBatchDisplayDriver> stringLocalizer)
    {
        _displayNameProvider = displayNameProvider;
        _contentDefinitionManager = contentDefinitionManager;
        _contentTypeProvider = contentTypeProvider;
        _timeZoneSelectListProvider = timeZoneSelectListProvider;
        _localClock = localClock;
        _session = session;
        _dispositionsCatalog = dispositionsCatalog;
        _campaignCatalog = campaignCatalog;
        _cadenceCatalog = cadenceCatalog;
        _channelEndpointsCatalog = channelEndpointsCatalog;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _optionsProvider = optionsProvider;
        _activityBatchSourceOptions = activityBatchSourceOptions.Value;
        _activityChannelOptions = activityChannelOptions.Value;
        _aiProfileManager = aiProfileManagers.FirstOrDefault();
        _businessHoursGate = businessHoursGate;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(OmnichannelActivityBatch batch, BuildDisplayContext context)
    {
        var results = new List<IDisplayResult>()
        {
            View("OmnichannelActivityBatch_Fields_SummaryAdmin", batch).Location("Content:1"),
            View("OmnichannelActivityBatch_Buttons_SummaryAdmin", batch).Location("Actions:5"),
            View("OmnichannelActivityBatch_DefaultMeta_SummaryAdmin", batch).Location("Meta:5"),

            // Every batch can be cloned, whatever its status; the view offers Load activities only to a new one.
            View("OmnichannelActivityBatch_ActionsMenuItems_SummaryAdmin", batch).Location("ActionsMenu:10"),
        };

        if (HasLoadReport(batch))
        {
            results.Add(View("OmnichannelActivityBatch_LoadReport", batch).Location("Description:5"));
        }

        return CombineAsync(results);
    }

    public override IDisplayResult Edit(OmnichannelActivityBatch batch, BuildEditorContext context)
    {
        var editor = BuildEditor(batch, context);

        if (!HasLoadReport(batch))
        {
            return editor;
        }

        // The editor is read-only once a batch has loaded, so the report of what the load did sits above it: it is
        // the first thing somebody opening a loaded batch wants to know.
        return Combine(
            View("OmnichannelActivityBatch_LoadReport", batch).Location("Content:0"),
            editor);
    }

    /// <summary>
    /// Whether the batch finished a load that recorded why each matching contact was or was not loaded. Batches loaded
    /// before the counts existed have none to show.
    /// </summary>
    private static bool HasLoadReport(OmnichannelActivityBatch batch)
        => batch.Status == OmnichannelActivityBatchStatus.Loaded && batch.TotalMatched.HasValue;

    private ShapeResult BuildEditor(OmnichannelActivityBatch batch, BuildEditorContext context)
    {
        return Initialize<OmnichannelActivityBatchViewModel>("OmnichannelActivityBatchFields_Edit", async model =>
        {
            model.DisplayText = batch.DisplayText;
            model.Source = string.IsNullOrEmpty(batch.Source) ? ActivitySources.Manual : batch.Source;
            model.SourceDisplayName = GetSourceEntry(model.Source)?.DisplayName.Value ?? model.Source;
            model.RequiresUserAssignment = GetSourceEntry(model.Source)?.RequiresUserAssignment ?? true;
            model.ScheduleAt = context.IsNew ? (await _localClock.GetLocalNowAsync()).DateTime : batch.ScheduleAt;
            model.SubjectContentType = batch.SubjectContentType;
            model.ContactContentType = batch.ContactContentType;
            model.CampaignId = batch.CampaignId;
            model.Channel = batch.Channel;
            model.ChannelEndpointId = batch.ChannelEndpointId;
            model.IsDialerSource = string.Equals(model.Source, ActivitySources.Dialer, StringComparison.OrdinalIgnoreCase);
            model.DialerProfileId = batch.DialerProfileId;
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

            await _contentTypeProvider.EnsureInitializedAsync(_contentDefinitionManager);

            // With the CRM feature on, the picker shows contacts and leads in their own groups, so choosing a list of
            // leads is a deliberate choice. Without it there are no lead types and the list reads as it always did.
            var leadGroup = new SelectListGroup { Name = S["Leads"] };
            var contactGroup = new SelectListGroup { Name = S["Contacts"] };
            var hasLeadTypes = _contentTypeProvider.GetLeadContentTypes().Count > 0;

            foreach (var contentType in await _contentDefinitionManager.ListTypeDefinitionsAsync())
            {
                if (_contentTypeProvider.IsContactContentType(contentType.Name))
                {
                    contactContentTypes.Add(new SelectListItem(contentType.DisplayName, contentType.Name)
                    {
                        Group = !hasLeadTypes ? null : _contentTypeProvider.IsLeadContentType(contentType.Name) ? leadGroup : contactGroup,
                    });
                }
            }

            model.DialerProfiles = await _optionsProvider.GetDialerProfileOptionsAsync(model.DialerProfileId, "Select a dialer profile");

            if (model.RequiresUserAssignment && batch.UserIds is { Length: > 0 })
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
                new(S["Contains"], nameof(PhoneNumberMatchType.Contains)),
                new(S["Exact match"], nameof(PhoneNumberMatchType.Exact)),
                new(S["Begins with"], nameof(PhoneNumberMatchType.BeginsWith)),
                new(S["Ends with"], nameof(PhoneNumberMatchType.EndsWith)),
            ];

            model.TimeZones = (await _timeZoneSelectListProvider.GetTimeZoneSelectListAsync())
                .Select(x => new SelectListItem(x.Value, x.Key)
                {
                    Selected = model.TimeZoneIds?.Contains(x.Key, StringComparer.OrdinalIgnoreCase) == true,
                });

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
            model.ContactContentTypes = contactContentTypes
                .OrderBy(x => x.Group == null ? string.Empty : x.Group.Name)
                .ThenBy(x => x.Text);

            var campaignItems = new List<SelectListItem>
            {
                new(S["No campaign"], ""),
            };

            foreach (var campaign in (await _campaignCatalog.GetAllAsync()).OrderBy(campaign => campaign.DisplayText))
            {
                campaignItems.Add(new SelectListItem(campaign.DisplayText, campaign.ItemId));
            }

            model.Campaigns = campaignItems;

            var isAutomaticSource = string.Equals(model.Source, ActivitySources.Automatic, StringComparison.OrdinalIgnoreCase);
            model.ShowAIProfile = isAutomaticSource && _aiProfileManager is not null;

            if (model.ShowAIProfile)
            {
                model.AIProfileId = batch.AIProfileId;
                model.AIProfiles = await GetAIProfileOptionsAsync(batch.AIProfileId);
                model.AllowAIToUpdateContact = batch.AllowAIToUpdateContact;
                model.AllowAIToUpdateSubject = batch.AllowAIToUpdateSubject;
                model.UseCallAmbience = batch.UseCallAmbience;
                model.ResponseDelayMode = batch.ResponseDelayMode;
                model.ResponseDelaySeconds = batch.ResponseDelaySeconds;
                model.ResponseDelayJitterSeconds = batch.ResponseDelayJitterSeconds;
                model.ResponseDelayModes =
                [
                    new(S["No delay"], nameof(OmnichannelResponseDelayMode.None)),
                    new(S["Fixed"], nameof(OmnichannelResponseDelayMode.Fixed)),
                    new(S["Random (base ± jitter)"], nameof(OmnichannelResponseDelayMode.Random)),
                ];

                model.CadenceId = batch.CadenceId;

                var cadenceItems = new List<SelectListItem>
                {
                    new(S["No follow-up cadence"], ""),
                };

                foreach (var schedule in (await _cadenceCatalog.GetAllAsync())
                    .Where(schedule => schedule.Enabled)
                    .OrderBy(schedule => schedule.DisplayText))
                {
                    cadenceItems.Add(new SelectListItem(schedule.DisplayText ?? schedule.ItemId, schedule.ItemId)
                    {
                        Selected = string.Equals(schedule.ItemId, batch.CadenceId, StringComparison.OrdinalIgnoreCase),
                    });
                }

                model.Cadences = cadenceItems;
                model.BusinessHoursCalendarId = batch.BusinessHoursCalendarId;

                // The business-hours calendar picker is available only when a feature provides calendars (ContactCenter).
                // A tenant without the business-hours feature has no calendars, and neither has one that has the
                // feature but has defined none. Both cases hide the picker rather than showing one with nothing
                // in it, and both treat conversations as always open.
                var calendars = await _businessHoursGate.GetCalendarOptionsAsync();

                var calendarItems = new List<SelectListItem>
                {
                    new(S["Always open (no restriction)"], ""),
                };

                foreach (var calendar in calendars)
                {
                    calendarItems.Add(new SelectListItem(calendar.Name, calendar.Id)
                    {
                        Selected = string.Equals(calendar.Id, batch.BusinessHoursCalendarId, StringComparison.OrdinalIgnoreCase),
                    });
                }

                model.BusinessHoursCalendars = calendarItems;
                model.ShowBusinessHoursCalendar = calendars.Count > 0;
            }

            // The channels the enabled features create activities on: Phone and SMS always, and each channel feature's
            // own (Email, for one) when it is enabled.
            model.Channels = _activityChannelOptions.Channels.Values
                .Select(channel => new SelectListItem(channel.DisplayName.Value, channel.Channel))
                .ToList();

            var isDialerLoad = string.Equals(model.Source, ActivitySources.Dialer, StringComparison.OrdinalIgnoreCase);
            var channelEndpointItems = new List<SelectListItem>
            {
                new(isDialerLoad ? S["Default caller ID"] : S["No address"], ""),
            };

            // Only addresses used on one of those channels can reach contacts on a load's channel, and a dialer load only
            // calls. Each address says what it is used for, so the editor offers only those used for the channel picked.
            foreach (var endpoint in (await _channelEndpointsCatalog.GetAllAsync())
                .Where(endpoint => endpoint.HasCapability(OmnichannelConstants.Channels.Phone) ||
                    (!isDialerLoad && _activityChannelOptions.Channels.Keys.Any(endpoint.HasCapability)))
                .OrderBy(endpoint => endpoint.DisplayText))
            {
                var text = string.IsNullOrWhiteSpace(endpoint.DisplayText) || endpoint.DisplayText == endpoint.Value
                    ? endpoint.Value
                    : $"{endpoint.DisplayText} ({endpoint.Value})";
                channelEndpointItems.Add(new SelectListItem(text, endpoint.ItemId, endpoint.ItemId == model.ChannelEndpointId));
                model.ChannelEndpointCapabilities[endpoint.ItemId] = string.Join(",", endpoint.GetCapabilities());
            }

            model.ChannelEndpoints = channelEndpointItems;

            model.SelectedUsers ??= [];
        }).Location("Content:1")
        .Processing<OmnichannelActivityBatchViewModel>(model =>
        {
            // Read when the editor renders rather than when it is built, so every driver has placed its shapes by then.
            model.RecordFilters = FindRecordFilters(context.Shape);

            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Returns the editor's <see cref="RecordFiltersZone"/> once a driver has placed a shape in it, or
    /// <see langword="null"/> while it is empty, so the card shows nothing extra when no record filters apply.
    /// </summary>
    /// <param name="editor">The editor shape the drivers place their shapes on.</param>
    internal static IShape FindRecordFilters(IShape editor)
        => editor is IZoneHolding zones && zones.Zones[RecordFiltersZone] is { } zone and not ZoneOnDemand
            ? zone
            : null;

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelActivityBatch batch, UpdateEditorContext context)
    {
        var model = new OmnichannelActivityBatchViewModel();
        model.Source = string.IsNullOrEmpty(batch.Source) ? ActivitySources.Manual : batch.Source;

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        model.Source = string.IsNullOrEmpty(model.Source) ? batch.Source : model.Source;
        model.Source = string.IsNullOrEmpty(model.Source) ? ActivitySources.Manual : model.Source;

        var sourceEntry = GetSourceEntry(model.Source);

        if (sourceEntry is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Source), S["The selected activity source is invalid."]);
        }

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
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SubjectContentType), S["The selected subject is invalid."]);
        }

        if (string.IsNullOrEmpty(model.ContactContentType))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ContactContentType), S["Record type is required."]);
        }

        if ((sourceEntry?.RequiresUserAssignment ?? true) && (model.UserIds is null || model.UserIds.Length == 0))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.UserIds), S["At least one user is required."]);
        }

        if (string.Equals(model.Source, ActivitySources.Dialer, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(model.DialerProfileId))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.DialerProfileId), S["Dialer profile is required for dialer activity loads."]);
            }

            if (!string.IsNullOrWhiteSpace(model.ChannelEndpointId))
            {
                var endpoint = await _channelEndpointsCatalog.FindByIdAsync(model.ChannelEndpointId);

                // The calls show the number, which only works for one the business uses for calls.
                if (endpoint is null || !endpoint.HasCapability(OmnichannelConstants.Channels.Phone))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ChannelEndpointId), S["Pick a number used for voice calls to dial from."]);
                }
            }
            else if (!await _optionsProvider.DialerProfileExistsAsync(model.DialerProfileId))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.DialerProfileId), S["The selected dialer profile is invalid."]);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(model.Channel))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Channel), S["A channel is required to load activities."]);
            }
            else if (!IsKnownChannel(model.Channel))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Channel), S["The selected channel is invalid."]);
            }

            if (!string.IsNullOrWhiteSpace(model.ChannelEndpointId))
            {
                var endpoint = await _channelEndpointsCatalog.FindByIdAsync(model.ChannelEndpointId);

                if (endpoint is null)
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ChannelEndpointId), S["The selected address is invalid."]);
                }
                else if (!string.IsNullOrWhiteSpace(model.Channel) && !endpoint.HasCapability(model.Channel))
                {
                    // A load sends from its address on its channel, which only works if the address is used for that.
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ChannelEndpointId), S["{0} is not used for {1}. Tick it on the address first, or choose another address.", endpoint.DisplayText, model.Channel]);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(model.CampaignId) &&
            await _campaignCatalog.FindByIdAsync(model.CampaignId) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CampaignId), S["The selected campaign is invalid."]);
        }
        else if (string.Equals(model.Source, ActivitySources.Dialer, StringComparison.OrdinalIgnoreCase))
        {
            var dialerCampaignId = model.CampaignId;

            if (string.IsNullOrWhiteSpace(dialerCampaignId) && !string.IsNullOrWhiteSpace(model.SubjectContentType))
            {
                // Dialer activities are queued on their campaign's queue, and agents sign in to the campaign to
                // receive them, so a dialer load needs a campaign: its own, or the subject's default one.
                var subjectFlowSettings = await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(model.SubjectContentType);

                if (subjectFlowSettings is not null && string.IsNullOrWhiteSpace(subjectFlowSettings.CampaignId))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.CampaignId), S["A campaign is required for dialer activity loads because the selected subject has no default campaign."]);
                }

                dialerCampaignId = subjectFlowSettings?.CampaignId;
            }

            // A campaign's waiting records are worked under one dialer profile, so records loaded under another one
            // behind them were never dialed.
            var conflicts = await _optionsProvider.FindDialerProfileConflictsAsync(dialerCampaignId, model.DialerProfileId);

            if (conflicts.Count > 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.DialerProfileId), S["The campaign already has records waiting under {0}. A campaign's waiting records must all use one dialer profile, so load these with that profile or into another campaign.", DialerCampaignProfileGuard.Describe(conflicts)]);
            }
        }

        var isAutomatic = string.Equals(model.Source, ActivitySources.Automatic, StringComparison.OrdinalIgnoreCase);

        if (isAutomatic && _aiProfileManager is not null)
        {
            var flowSettings = !string.IsNullOrWhiteSpace(model.SubjectContentType)
                ? await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(model.SubjectContentType)
                : null;
            var selectedProfileId = string.IsNullOrWhiteSpace(model.AIProfileId)
                ? flowSettings?.ProfileId
                : model.AIProfileId.Trim();

            if (string.IsNullOrWhiteSpace(selectedProfileId))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.AIProfileId), S["AI profile is required for automatic activity loads."]);
            }
            else
            {
                var profile = await _aiProfileManager.FindByIdAsync(selectedProfileId);

                if (profile is null || profile.Type != AIProfileType.Chat)
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.AIProfileId), S["The selected AI profile is invalid."]);
                }
                else if (!HasInitialPrompt(profile))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.AIProfileId), S["The selected AI profile must have Start the conversation automatically enabled."]);
                }
            }

            batch.AIProfileId = model.AIProfileId?.Trim();
            batch.AllowAIToUpdateContact = model.AllowAIToUpdateContact;
            batch.AllowAIToUpdateSubject = model.AllowAIToUpdateSubject;
            batch.UseCallAmbience = model.UseCallAmbience;
            batch.ResponseDelayMode = model.ResponseDelayMode;
            batch.ResponseDelaySeconds = Math.Max(0, model.ResponseDelaySeconds);
            batch.ResponseDelayJitterSeconds = Math.Max(0, model.ResponseDelayJitterSeconds);

            batch.CadenceId = string.IsNullOrWhiteSpace(model.CadenceId)
                ? null
                : model.CadenceId.Trim();
            batch.BusinessHoursCalendarId = string.IsNullOrWhiteSpace(model.BusinessHoursCalendarId)
                ? null
                : model.BusinessHoursCalendarId.Trim();

            if (!string.IsNullOrWhiteSpace(batch.CadenceId) &&
                await _cadenceCatalog.FindByIdAsync(batch.CadenceId) is null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.CadenceId), S["The selected cadence is invalid."]);
            }
        }
        else
        {
            batch.AIProfileId = null;
        }

        if (model.ScheduleAt is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ScheduleAt), S["Schedule at field is required."]);
        }

        if (!string.IsNullOrWhiteSpace(model.PhoneNumber) &&
            !PhoneNumberSearchTerm.TryParse(model.PhoneNumber, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PhoneNumber), S["Phone number must contain at least one digit."]);
        }

        var isDialer = string.Equals(model.Source, ActivitySources.Dialer, StringComparison.OrdinalIgnoreCase);

        batch.DisplayText = model.DisplayText?.Trim();
        batch.Source = model.Source?.Trim();
        batch.SubjectContentType = model.SubjectContentType;
        batch.ContactContentType = model.ContactContentType;
        batch.CampaignId = string.IsNullOrWhiteSpace(model.CampaignId) ? null : model.CampaignId.Trim();
        batch.Channel = isDialer || string.IsNullOrWhiteSpace(model.Channel) ? null : model.Channel.Trim();
        batch.ChannelEndpointId = string.IsNullOrWhiteSpace(model.ChannelEndpointId) ? null : model.ChannelEndpointId.Trim();
        batch.DialerProfileId = isDialer
            ? model.DialerProfileId?.Trim()
            : null;

        batch.Instructions = model.Instructions?.Trim();
        batch.UrgencyLevel = model.UrgencyLevel;
        batch.UserIds = sourceEntry?.RequiresUserAssignment == true ? model.UserIds ?? [] : [];
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

    private async Task<IEnumerable<SelectListItem>> GetAIProfileOptionsAsync(string selectedProfileId)
    {
        var chatProfiles = await _aiProfileManager.GetAsync(AIProfileType.Chat);

        return chatProfiles
            .Where(HasInitialPrompt)
            .OrderBy(profile => profile.DisplayText ?? profile.Name, StringComparer.OrdinalIgnoreCase)
            .Select(profile => new SelectListItem(profile.DisplayText ?? profile.Name, profile.ItemId)
            {
                Selected = string.Equals(profile.ItemId, selectedProfileId, StringComparison.OrdinalIgnoreCase),
            });
    }

    private static bool HasInitialPrompt(AIProfile profile)
    {
        var metadata = profile.GetOrCreate<AIProfileMetadata>();

        return !string.IsNullOrWhiteSpace(metadata.InitialPrompt);
    }

    private ActivityBatchSourceEntry GetSourceEntry(string source)
    {
        var normalizedSource = string.IsNullOrWhiteSpace(source)
            ? ActivitySources.Manual
            : source.Trim();

        _activityBatchSourceOptions.Sources.TryGetValue(normalizedSource, out var entry);

        return entry;
    }

    // The editor offers the channels the enabled features create activities on, so any other value is refused.
    private bool IsKnownChannel(string channel)
        => _activityChannelOptions.Channels.ContainsKey(channel);
}
