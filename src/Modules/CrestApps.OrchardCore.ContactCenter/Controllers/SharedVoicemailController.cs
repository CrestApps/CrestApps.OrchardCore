using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// The shared voicemail page: the messages left in the shared voicemail boxes of the queues the viewer may see, and
/// what the team does with them.
/// </summary>
/// <remarks>
/// Every action goes through <see cref="ISharedVoicemailService"/>, which authorizes it against the viewer's
/// permission and queue entitlements. Nothing here decides access on its own, so a request crafted outside this page
/// is refused exactly as the page would refuse it.
/// </remarks>
[Admin]
[Feature(ContactCenterConstants.Feature.InboundVoice)]
public sealed class SharedVoicemailController : Controller
{
    private const string IndexRouteName = "ContactCenterSharedVoicemailIndex";

    private readonly ISharedVoicemailService _sharedVoicemailService;
    private readonly ISharedVoicemailAuthorizationService _authorizationService;
    private readonly IActivityQueueManager _queueManager;
    private readonly IInteractionManager _interactionManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedVoicemailController"/> class.
    /// </summary>
    /// <param name="sharedVoicemailService">The service that lists and acts on shared voicemails.</param>
    /// <param name="authorizationService">The authorization that decides which boxes the viewer may see.</param>
    /// <param name="queueManager">The queue manager used to name the queues.</param>
    /// <param name="interactionManager">The interaction manager used to read each message's recording and length.</param>
    /// <param name="notifier">The admin notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public SharedVoicemailController(
        ISharedVoicemailService sharedVoicemailService,
        ISharedVoicemailAuthorizationService authorizationService,
        IActivityQueueManager queueManager,
        IInteractionManager interactionManager,
        INotifier notifier,
        IHtmlLocalizer<SharedVoicemailController> htmlLocalizer)
    {
        _sharedVoicemailService = sharedVoicemailService;
        _authorizationService = authorizationService;
        _queueManager = queueManager;
        _interactionManager = interactionManager;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the shared voicemails the viewer may see.
    /// </summary>
    /// <param name="queueId">The queue to filter to, or empty for every queue the viewer may see.</param>
    /// <param name="status">Which messages to show by their handling.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list view.</returns>
    [Admin("contact-center/shared-voicemail", IndexRouteName)]
    public async Task<IActionResult> Index(
        string queueId,
        SharedVoicemailStatusFilter status,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        var access = await _authorizationService.GetAccessAsync(User, HttpContext.RequestAborted);

        if (!access.CanAccess)
        {
            return Forbid();
        }

        var queues = (await _queueManager.GetAllAsync(HttpContext.RequestAborted))
            .Where(queue => access.CoversQueue(queue.ItemId))
            .OrderBy(queue => queue.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var queueNames = queues.ToDictionary(queue => queue.ItemId, queue => queue.Name, StringComparer.OrdinalIgnoreCase);

        // A filter naming a queue the viewer may not see is dropped rather than obeyed; the service would read nothing
        // from it anyway.
        queueId = !string.IsNullOrWhiteSpace(queueId) && access.CoversQueue(queueId) ? queueId : null;

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());
        var query = new SharedVoicemailQuery
        {
            QueueIds = queueId is null ? null : [queueId],
            Page = pager.Page,
            PageSize = pager.PageSize,
        };
        ApplyStatus(query, status);

        var page = await _sharedVoicemailService.ListAsync(User, query, HttpContext.RequestAborted);
        var newMessages = await _sharedVoicemailService.ListAsync(
            User,
            new SharedVoicemailQuery { Status = SharedVoicemailStatus.New, PageSize = 1 },
            HttpContext.RequestAborted);

        var routeData = new RouteData();

        if (queueId is not null)
        {
            routeData.Values.TryAdd(nameof(queueId), queueId);
        }

        if (status != SharedVoicemailStatusFilter.Open)
        {
            routeData.Values.TryAdd(nameof(status), status.ToString());
        }

        var model = new SharedVoicemailListViewModel
        {
            QueueId = queueId,
            Status = status,
            NewCount = newMessages.Count,
            QueueOptions = queues
                .Select(queue => new SelectListItem(queue.Name ?? queue.ItemId, queue.ItemId, string.Equals(queue.ItemId, queueId, StringComparison.OrdinalIgnoreCase)))
                .ToList(),
            Pager = await shapeFactory.PagerAsync(pager, page.Count, routeData),
        };

        foreach (var voicemail in page.Entries)
        {
            model.Items.Add(await BuildItemAsync(voicemail, access, queueNames));
        }

        return View(model);
    }

    /// <summary>
    /// Streams a shared voicemail's recording.
    /// </summary>
    /// <param name="id">The shared voicemail identifier.</param>
    /// <returns>The recording, or not found.</returns>
    [Admin("contact-center/shared-voicemail/{id}/recording", "ContactCenterSharedVoicemailRecording")]
    public async Task<IActionResult> Recording(string id)
    {
        var result = await _sharedVoicemailService.OpenRecordingAsync(User, id, HttpContext.RequestAborted);

        // A recording the viewer may not hear is reported as one that is not there, so its existence is not disclosed.
        return result.Succeeded
            ? File(result.Recording, "audio/mpeg")
            : NotFound();
    }

    /// <summary>
    /// Claims a shared voicemail for the viewer.
    /// </summary>
    /// <param name="id">The shared voicemail identifier.</param>
    /// <param name="returnUrl">The list page to return to.</param>
    /// <returns>A redirect back to the list.</returns>
    [HttpPost]
    [Admin("contact-center/shared-voicemail/{id}/claim", "ContactCenterSharedVoicemailClaim")]
    public async Task<IActionResult> Claim(string id, string returnUrl)
    {
        var result = await _sharedVoicemailService.ClaimAsync(User, id, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["You are now handling this voicemail."]);
        }
        else
        {
            await NotifyFailureAsync(result);
        }

        return Return(returnUrl);
    }

    /// <summary>
    /// Returns a shared voicemail to the team, unclaimed.
    /// </summary>
    /// <param name="id">The shared voicemail identifier.</param>
    /// <param name="returnUrl">The list page to return to.</param>
    /// <returns>A redirect back to the list.</returns>
    [HttpPost]
    [Admin("contact-center/shared-voicemail/{id}/release", "ContactCenterSharedVoicemailRelease")]
    public async Task<IActionResult> Release(string id, string returnUrl)
    {
        var result = await _sharedVoicemailService.ReleaseAsync(User, id, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The voicemail was returned to the team."]);
        }
        else
        {
            await NotifyFailureAsync(result);
        }

        return Return(returnUrl);
    }

    /// <summary>
    /// Marks a shared voicemail as dealt with.
    /// </summary>
    /// <param name="id">The shared voicemail identifier.</param>
    /// <param name="note">An optional note on how the message was dealt with.</param>
    /// <param name="returnUrl">The list page to return to.</param>
    /// <returns>A redirect back to the list.</returns>
    [HttpPost]
    [Admin("contact-center/shared-voicemail/{id}/resolve", "ContactCenterSharedVoicemailResolve")]
    public async Task<IActionResult> Resolve(string id, string note, string returnUrl)
    {
        var result = await _sharedVoicemailService.ResolveAsync(User, id, note, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The voicemail was marked as done."]);
        }
        else
        {
            await NotifyFailureAsync(result);
        }

        return Return(returnUrl);
    }

    /// <summary>
    /// Schedules a callback to the caller who left a shared voicemail.
    /// </summary>
    /// <param name="id">The shared voicemail identifier.</param>
    /// <param name="returnUrl">The list page to return to.</param>
    /// <returns>A redirect back to the list.</returns>
    [HttpPost]
    [Admin("contact-center/shared-voicemail/{id}/call-back", "ContactCenterSharedVoicemailCallBack")]
    public async Task<IActionResult> CallBack(string id, string returnUrl)
    {
        var result = await _sharedVoicemailService.RequestCallbackAsync(User, id, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["A callback to the caller was queued. The next available agent in the queue will be offered it."]);
        }
        else
        {
            await NotifyFailureAsync(result);
        }

        return Return(returnUrl);
    }

    /// <summary>
    /// Deletes a shared voicemail and erases its recording.
    /// </summary>
    /// <param name="id">The shared voicemail identifier.</param>
    /// <param name="returnUrl">The list page to return to.</param>
    /// <returns>A redirect back to the list.</returns>
    [HttpPost]
    [Admin("contact-center/shared-voicemail/{id}/delete", "ContactCenterSharedVoicemailDelete")]
    public async Task<IActionResult> Delete(string id, string returnUrl)
    {
        var result = await _sharedVoicemailService.DeleteAsync(User, id, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The voicemail was deleted."]);
        }
        else
        {
            await NotifyFailureAsync(result);
        }

        return Return(returnUrl);
    }

    private static void ApplyStatus(SharedVoicemailQuery query, SharedVoicemailStatusFilter status)
    {
        switch (status)
        {
            case SharedVoicemailStatusFilter.New:
                query.Status = SharedVoicemailStatus.New;
                break;
            case SharedVoicemailStatusFilter.Claimed:
                query.Status = SharedVoicemailStatus.Claimed;
                break;
            case SharedVoicemailStatusFilter.Resolved:
                query.Status = SharedVoicemailStatus.Resolved;
                break;
            case SharedVoicemailStatusFilter.All:
                query.IncludeResolved = true;
                break;
        }
    }

    private async Task<SharedVoicemailItemViewModel> BuildItemAsync(
        SharedVoicemail voicemail,
        SharedVoicemailAccess access,
        Dictionary<string, string> queueNames)
    {
        var interaction = await _interactionManager.FindByIdAsync(voicemail.InteractionId, HttpContext.RequestAborted);

        return new SharedVoicemailItemViewModel
        {
            Voicemail = voicemail,
            QueueName = queueNames.TryGetValue(voicemail.QueueId, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : voicemail.QueueId,
            HasRecording = HasRecording(interaction),
            DurationSeconds = GetDurationSeconds(interaction, voicemail),
            IsMine = access.Holds(voicemail),
            CanClaim = access.CanClaim(voicemail),
            CanRelease = access.CanRelease(voicemail),
            CanResolve = access.CanWork(voicemail),
            CanCallBack = access.CanWork(voicemail) && !string.IsNullOrWhiteSpace(voicemail.CallerNumber),
            CanDelete = access.CanDelete(voicemail),
        };
    }

    private static bool HasRecording(Interaction interaction)
        => interaction is not null &&
            !string.IsNullOrEmpty(interaction.RecordingReference) &&
            interaction.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.RecordingMetadata.StorageReference, out var reference) &&
            !string.IsNullOrEmpty(reference?.ToString());

    // The provider's own length of the recording when it reported one; otherwise the time from the caller being sent to
    // voicemail to their hanging up, which includes the greeting but is the closest the platform knows.
    private static int? GetDurationSeconds(Interaction interaction, SharedVoicemail voicemail)
    {
        if (interaction?.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.RecordingMetadata.DurationSeconds, out var value) &&
            double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) &&
            seconds >= 0)
        {
            return (int)Math.Round(seconds);
        }

        if (interaction?.EndedUtc is { } endedUtc && voicemail.ReceivedUtc != default && endedUtc >= voicemail.ReceivedUtc)
        {
            return (int)Math.Round((endedUtc - voicemail.ReceivedUtc).TotalSeconds);
        }

        return null;
    }

    private async Task NotifyFailureAsync(SharedVoicemailActionResult result)
    {
        var message = result.ReasonCode switch
        {
            SharedVoicemailReasons.ClaimedByAnotherUser => H["{0} is already handling this voicemail.", result.Voicemail?.ClaimedByUserName ?? string.Empty],
            SharedVoicemailReasons.AlreadyResolved => H["This voicemail was already marked as done. Return it to the team to work it again."],
            SharedVoicemailReasons.ManagePermissionRequired => H["You are not allowed to delete shared voicemail."],
            SharedVoicemailReasons.NoCallerNumber => H["The caller left no number to call back."],
            SharedVoicemailReasons.CallbacksUnavailable => H["Callbacks are not available. Enable the Contact Center Outbound Dialer feature to queue callbacks."],
            SharedVoicemailReasons.LegalHold => H["The voicemail's recording is under legal hold and cannot be deleted."],
            _ when result.Status == SharedVoicemailActionStatus.NotFound => H["The voicemail could not be found."],
            _ => H["You are not allowed to do that with this voicemail."],
        };

        await _notifier.ErrorAsync(message);
    }

    private IActionResult Return(string returnUrl)
        => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToRoute(IndexRouteName);
}
