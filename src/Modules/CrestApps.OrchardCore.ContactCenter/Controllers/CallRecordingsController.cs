using System.Globalization;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// The call recordings page: search the recorded calls, play one back, and read its transcript.
/// </summary>
/// <remarks>
/// A user who may only hear their own calls is searched, listed and played only their own: the search is narrowed to
/// them whatever the query string says, and another user's recording is reported as not found, so its existence is
/// not disclosed. A user who may hear everyone's calls can search by agent.
/// </remarks>
[Admin]
[Feature(ContactCenterConstants.Feature.Recording)]
public sealed class CallRecordingsController : Controller
{
    private const string IndexRouteName = "ContactCenterCallRecordingsIndex";
    private const string DisplayRouteName = "ContactCenterCallRecordingsDisplay";

    // The machine format the date range picker reads and writes.
    private const string DateRangeRouteFormat = "yyyy-MM-ddTHH:mm";

    private readonly ICallRecordingStore _store;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<ICallRecordingTranscriptProvider> _transcriptProviders;
    private readonly IRecordingAccessGovernanceService _governance;
    private readonly IInteractionManager _interactionManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IRecordingMediaStore _mediaStore;
    private readonly CallRecordingAccessEvaluator _accessEvaluator;
    private readonly CallRecordingAgentNameResolver _nameResolver;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly ISiteService _siteService;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;
    private readonly INotifier _notifier;
    private readonly ILogger _logger;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallRecordingsController"/> class.
    /// </summary>
    /// <param name="store">The call recordings catalog.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="transcriptProviders">The sources of a call's transcript.</param>
    /// <param name="governance">The recording governance that audits every playback and erases interaction recordings.</param>
    /// <param name="interactionManager">The interaction manager, read for a recording's legal hold.</param>
    /// <param name="activityManager">The CRM activity manager, read for the contact a call was with.</param>
    /// <param name="mediaStores">The encrypted recording media store, when telephony registered one.</param>
    /// <param name="accessEvaluator">Works out which recordings the viewer may hear.</param>
    /// <param name="nameResolver">Names the agents on a page of calls in one query.</param>
    /// <param name="phoneNumberService">The phone number service, which formats the customer's number for reading.</param>
    /// <param name="agentProfileManagers">The agent profile manager, when agents are enabled, listing the agents the page can be narrowed to.</param>
    /// <param name="siteService">The site service, read for the region phone numbers are shown in.</param>
    /// <param name="localClock">The viewer's local clock, which the date filters are in.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="notifier">The admin notifier.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CallRecordingsController(
        ICallRecordingStore store,
        IAuthorizationService authorizationService,
        IEnumerable<ICallRecordingTranscriptProvider> transcriptProviders,
        IRecordingAccessGovernanceService governance,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        IEnumerable<IRecordingMediaStore> mediaStores,
        CallRecordingAccessEvaluator accessEvaluator,
        CallRecordingAgentNameResolver nameResolver,
        IPhoneNumberService phoneNumberService,
        IEnumerable<IAgentProfileManager> agentProfileManagers,
        ISiteService siteService,
        ILocalClock localClock,
        IClock clock,
        INotifier notifier,
        ILogger<CallRecordingsController> logger,
        IHtmlLocalizer<CallRecordingsController> htmlLocalizer,
        IStringLocalizer<CallRecordingsController> stringLocalizer)
    {
        _store = store;
        _authorizationService = authorizationService;
        _transcriptProviders = transcriptProviders;
        _governance = governance;
        _interactionManager = interactionManager;
        _activityManager = activityManager;
        _mediaStore = mediaStores.LastOrDefault();
        _accessEvaluator = accessEvaluator;
        _nameResolver = nameResolver;
        _phoneNumberService = phoneNumberService;
        _agentProfileManager = agentProfileManagers.FirstOrDefault();
        _siteService = siteService;
        _localClock = localClock;
        _clock = clock;
        _notifier = notifier;
        _logger = logger;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Searches the recorded calls the viewer may hear.
    /// </summary>
    /// <param name="filter">The search.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list view.</returns>
    [Admin("contact-center/call-recordings", IndexRouteName)]
    public async Task<IActionResult> Index(
        CallRecordingFilterViewModel filter,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        var access = await GetAccessAsync();

        if (!access.CanListOwn)
        {
            return Forbid();
        }

        filter ??= new CallRecordingFilterViewModel();

        if (!access.CanListEveryone)
        {
            filter.AgentUserId = null;
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var query = new CallRecordingQuery
        {
            // Someone who may only hear their own calls is only ever searched their own, whatever was asked for.
            AgentUserId = access.CanListEveryone ? NullIfEmpty(filter.AgentUserId) : access.UserId,
            CustomerAddress = NullIfEmpty(filter.Number),
            Direction = filter.Direction,
            Source = filter.Source,
            FromUtc = filter.From is { } from ? await ToUtcAsync(TruncateToMinute(from)) : null,

            // The picker's upper bound is inclusive to the minute; the query's is exclusive.
            ToUtc = filter.To is { } to ? await ToUtcAsync(TruncateToMinute(to).AddMinutes(1)) : null,
            Page = pager.Page,
            PageSize = pager.PageSize,
        };

        var page = await _store.QueryAsync(query, HttpContext.RequestAborted);
        var names = await GetUserNamesAsync(page.Entries.Select(recording => recording.AgentUserId));
        var region = await GetPhoneRegionAsync();

        var routeData = new RouteData();
        AddRouteValue(routeData, nameof(filter.From), filter.From?.ToString(DateRangeRouteFormat, CultureInfo.InvariantCulture));
        AddRouteValue(routeData, nameof(filter.To), filter.To?.ToString(DateRangeRouteFormat, CultureInfo.InvariantCulture));
        AddRouteValue(routeData, nameof(filter.Range), filter.Range);
        AddRouteValue(routeData, nameof(filter.Number), filter.Number);
        AddRouteValue(routeData, nameof(filter.Direction), filter.Direction?.ToString());
        AddRouteValue(routeData, nameof(filter.Source), filter.Source?.ToString());
        AddRouteValue(routeData, nameof(filter.AgentUserId), filter.AgentUserId);

        var model = new CallRecordingListViewModel
        {
            Filter = filter,
            CanListEveryone = access.CanListEveryone,
            Count = page.Count,
            DirectionOptions =
            [
                new SelectListItem(S["Inbound"], nameof(InteractionDirection.Inbound), filter.Direction == InteractionDirection.Inbound),
                new SelectListItem(S["Outbound"], nameof(InteractionDirection.Outbound), filter.Direction == InteractionDirection.Outbound),
            ],
            SourceOptions =
            [
                new SelectListItem(S["Contact Center call"], nameof(CallRecordingSource.ContactCenter), filter.Source == CallRecordingSource.ContactCenter),
                new SelectListItem(S["AI voice agent"], nameof(CallRecordingSource.AiAgent), filter.Source == CallRecordingSource.AiAgent),
                new SelectListItem(S["Soft phone keypad"], nameof(CallRecordingSource.SoftPhone), filter.Source == CallRecordingSource.SoftPhone),
            ],
            Pager = await shapeFactory.PagerAsync(pager, page.Count, routeData),
        };

        if (access.CanListEveryone)
        {
            model.AgentOptions = await GetAgentOptionsAsync(filter.AgentUserId);
        }

        foreach (var recording in page.Entries)
        {
            model.Items.Add(new CallRecordingListItemViewModel
            {
                Recording = recording,
                AgentName = NameOf(recording.AgentUserId, names),
                CustomerNumber = FormatNumber(recording.CustomerAddress, region),
            });
        }

        return View(model);
    }

    /// <summary>
    /// Shows one recorded call: its details, the player, and its transcript.
    /// </summary>
    /// <param name="id">The recording identifier.</param>
    /// <returns>The recording view, or not found.</returns>
    [Admin("contact-center/call-recordings/{id}", DisplayRouteName)]
    public async Task<IActionResult> Display(string id)
    {
        var access = await GetAccessAsync();

        if (!access.CanListOwn)
        {
            return Forbid();
        }

        var recording = await FindPlayableAsync(id, access);

        if (recording is null)
        {
            return NotFound();
        }

        // Opening the page shows the transcript, which is the recording's content too. Listening is audited by the
        // media endpoint when playback starts.
        await AuditAccessAsync(recording, access, "call-recordings-view");

        var names = await GetUserNamesAsync([recording.AgentUserId]);
        var interaction = string.IsNullOrEmpty(recording.InteractionId)
            ? null
            : await _interactionManager.FindByIdAsync(recording.InteractionId, HttpContext.RequestAborted);
        var activity = string.IsNullOrEmpty(recording.ActivityItemId)
            ? null
            : await _activityManager.FindByIdAsync(recording.ActivityItemId, HttpContext.RequestAborted);

        return View(new CallRecordingDisplayViewModel
        {
            Recording = recording,
            AgentName = NameOf(recording.AgentUserId, names),
            CustomerNumber = FormatNumber(recording.CustomerAddress, await GetPhoneRegionAsync()),
            Transcript = await GetTranscriptAsync(recording),
            ContactContentItemId = activity?.ContactContentItemId,
            IsUnderLegalHold = interaction?.RecordingLegalHold == true,
            CanErase = await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.DeleteCallRecordings),
        });
    }

    /// <summary>
    /// Streams a recording's audio. Byte ranges are honored, so the player can seek to any line of the transcript.
    /// </summary>
    /// <param name="id">The recording identifier.</param>
    /// <returns>The audio, or not found.</returns>
    [Admin("contact-center/call-recordings/{id}/media", "ContactCenterCallRecordingsMedia")]
    public async Task<IActionResult> Media(string id)
    {
        var access = await GetAccessAsync();

        if (!access.CanListOwn)
        {
            return Forbid();
        }

        var recording = await FindPlayableAsync(id, access);

        if (recording is null || _mediaStore is null)
        {
            return NotFound();
        }

        // A player asks for the start of the recording when it begins playing and for later ranges as it seeks, so
        // only a request from the start is audited: once per playback, from the list or the call page alike.
        if (IsPlaybackStart(Request.Headers.Range.ToString()))
        {
            await AuditAccessAsync(recording, access, "call-recordings-playback");
        }

        var source = await _mediaStore.OpenReadAsync(recording.StorageReference, HttpContext.RequestAborted);

        if (source is null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Call recording '{RecordingId}' is listed as stored, but the media store has nothing under its reference.",
                    recording.ItemId.SanitizeLogValue());
            }

            return NotFound();
        }

        // The store decrypts forward only, but a player seeks with byte ranges. The recording is decrypted into memory
        // (a temporary file only for very long calls) that the response streams ranges from, and released with it.
        var media = await SeekableRecordingMedia.CreateAsync(source, HttpContext.RequestAborted);

        Response.Headers.CacheControl = "private, no-store";

        return File(media, ContentTypeOf(recording.Format), enableRangeProcessing: true);
    }

    /// <summary>
    /// Erases a recording: its media is deleted and it is no longer listed. An interaction's recording is erased
    /// through recording governance, which refuses a recording under legal hold and audits the erasure.
    /// </summary>
    /// <param name="id">The recording identifier.</param>
    /// <param name="reason">Why the recording is erased, kept on the audit trail.</param>
    /// <returns>A redirect to the list, or back to the recording when it was not erased.</returns>
    [HttpPost]
    [Admin("contact-center/call-recordings/{id}/erase", "ContactCenterCallRecordingsErase")]
    public async Task<IActionResult> Erase(string id, string reason)
    {
        var access = await GetAccessAsync();

        if (!access.CanListOwn || !await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.DeleteCallRecordings))
        {
            return Forbid();
        }

        var recording = await FindPlayableAsync(id, access);

        if (recording is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            await _notifier.ErrorAsync(H["Enter why the recording is being erased."]);

            return RedirectToRoute(DisplayRouteName, new { id });
        }

        reason = reason.Trim();

        if (!string.IsNullOrEmpty(recording.InteractionId))
        {
            // Durable writes that must not be torn by a viewer who navigates away, like the erasure endpoint's.
            var decision = await _governance.EraseAsync(
                recording.InteractionId,
                ContactCenterActor.Supervisor(access.UserId),
                reason,
                CancellationToken.None);

            if (decision.Erased)
            {
                // The erasure event takes every recording of the interaction off the page; this one goes at once.
                recording.ErasedUtc = _clock.UtcNow;
                await _store.UpdateAsync(recording, CancellationToken.None);

                LogErased(recording, access);
                await _notifier.SuccessAsync(H["The recording was erased."]);

                return RedirectToRoute(IndexRouteName);
            }

            // A hold is checked on the interaction too: it no longer naming a recording does not lift it.
            var interaction = await _interactionManager.FindByIdAsync(recording.InteractionId, CancellationToken.None);

            if (decision.DenyReasonCode == ContactCenterConstants.RecordingErasureDenyReason.LegalHold ||
                interaction?.RecordingLegalHold == true)
            {
                await _notifier.ErrorAsync(H["The recording is under legal hold and cannot be erased."]);

                return RedirectToRoute(DisplayRouteName, new { id });
            }

            // The interaction no longer names a recording: it was erased already, or this one was never attached to
            // it. The entry's own media is erased below.
        }

        if (_mediaStore is null || !await _mediaStore.DeleteAsync(recording.StorageReference, CancellationToken.None))
        {
            await _notifier.ErrorAsync(H["The recording could not be erased. Try again in a moment."]);

            return RedirectToRoute(DisplayRouteName, new { id });
        }

        recording.ErasedUtc = _clock.UtcNow;
        await _store.UpdateAsync(recording, CancellationToken.None);

        LogErased(recording, access);
        await _notifier.SuccessAsync(H["The recording was erased."]);

        return RedirectToRoute(IndexRouteName);
    }

    private static bool IsPlaybackStart(string range)
        => string.IsNullOrWhiteSpace(range) ||
            range.Trim().StartsWith("bytes=0-", StringComparison.OrdinalIgnoreCase);

    private Task<CallRecordingAccess> GetAccessAsync()
        => _accessEvaluator.GetAccessAsync(User);

    // A recording the viewer may not hear, or that cannot be played, is reported as one that is not there.
    private async Task<CallRecording> FindPlayableAsync(string id, CallRecordingAccess access)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var recording = await _store.FindByIdAsync(id, HttpContext.RequestAborted);

        return CallRecordingAccess.IsPlayable(recording) && access.CanHear(recording)
            ? recording
            : null;
    }

    private async Task AuditAccessAsync(CallRecording recording, CallRecordingAccess access, string purpose)
    {
        var actor = access.CanListEveryone && !string.Equals(recording.AgentUserId, access.UserId, StringComparison.Ordinal)
            ? ContactCenterActor.Supervisor(access.UserId)
            : ContactCenterActor.Agent(access.UserId);

        // An interaction's recording is listened to on the interaction's own recording-access trail. A recording
        // that belongs to no interaction has no trail to join, so the access is logged.
        if (!string.IsNullOrEmpty(recording.InteractionId) &&
            await _governance.RecordAccessAsync(recording.InteractionId, actor, purpose, HttpContext.RequestAborted))
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "User '{UserId}' opened call recording '{RecordingId}' ({Source}, agent '{AgentUserId}') for {Purpose}.",
                access.UserId.SanitizeLogValue(),
                recording.ItemId.SanitizeLogValue(),
                recording.Source,
                recording.AgentUserId.SanitizeLogValue(),
                purpose);
        }
    }

    private async Task<CallRecordingTranscript> GetTranscriptAsync(CallRecording recording)
    {
        foreach (var provider in _transcriptProviders)
        {
            try
            {
                var transcript = await provider.GetTranscriptAsync(recording, HttpContext.RequestAborted);

                if (transcript is not null && transcript.Phrases.Count > 0)
                {
                    return transcript;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The recording still plays without its transcript.
                _logger.LogWarning(ex, "The transcript of call recording '{RecordingId}' could not be read.", recording.ItemId.SanitizeLogValue());
            }
        }

        return null;
    }

    private async Task<DateTime> ToUtcAsync(DateTime localTime)
        => DateTime.SpecifyKind(await _localClock.ConvertToUtcAsync(DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified)), DateTimeKind.Utc);

    private static DateTime TruncateToMinute(DateTime value)
        => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, DateTimeKind.Unspecified);

    // The region the soft phone dials in is the tenant's home region for phone numbers: a number from there is shown
    // the way it is said locally, and one from anywhere else keeps its country code.
    private async Task<string> GetPhoneRegionAsync()
    {
        var settings = await _siteService.GetSettingsAsync<SoftPhoneWidgetSettings>();

        if (!string.IsNullOrWhiteSpace(settings?.DefaultCountryCode))
        {
            return settings.DefaultCountryCode.Trim().ToUpperInvariant();
        }

        try
        {
            return new RegionInfo(CultureInfo.CurrentCulture.Name).TwoLetterISORegionName;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private string FormatNumber(string number, string region)
        => string.IsNullOrWhiteSpace(number) ? null : _phoneNumberService.FormatForDisplay(number.Trim(), region);

    // The agents the list can be narrowed to: every Contact Center agent, named as the site names its users.
    private async Task<List<SelectListItem>> GetAgentOptionsAsync(string selectedUserId)
    {
        if (_agentProfileManager is null)
        {
            return [];
        }

        var agents = (await _agentProfileManager.GetAllAsync())
            .Where(agent => !string.IsNullOrWhiteSpace(agent.UserId))
            .ToArray();
        var names = await GetUserNamesAsync(agents.Select(agent => agent.UserId));

        return agents
            .Select(agent => new SelectListItem(
                names.TryGetValue(agent.UserId, out var name)
                    ? name
                    : agent.DisplayName ?? agent.UserName ?? agent.UserId,
                agent.UserId,
                string.Equals(agent.UserId, selectedUserId, StringComparison.Ordinal)))
            .DistinctBy(option => option.Value, StringComparer.Ordinal)
            .OrderBy(option => option.Text, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private Task<Dictionary<string, string>> GetUserNamesAsync(IEnumerable<string> userIds)
        => _nameResolver.GetNamesAsync(userIds, HttpContext.RequestAborted);

    private void LogErased(CallRecording recording, CallRecordingAccess access)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "User '{UserId}' erased call recording '{RecordingId}' (interaction '{InteractionId}').",
                access.UserId.SanitizeLogValue(),
                recording.ItemId.SanitizeLogValue(),
                recording.InteractionId.SanitizeLogValue());
        }
    }

    private static string NameOf(string userId, Dictionary<string, string> names)
        => CallRecordingAgentNameResolver.NameOf(userId, names);

    private static string NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void AddRouteValue(RouteData routeData, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            routeData.Values.TryAdd(key, value);
        }
    }

    internal static string ContentTypeOf(string format)
        => (format ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant() switch
        {
            "wav" or "wave" => "audio/wav",
            "ogg" or "opus" => "audio/ogg",
            "webm" => "audio/webm",
            "m4a" or "mp4" or "aac" => "audio/mp4",
            _ => "audio/mpeg",
        };

}
