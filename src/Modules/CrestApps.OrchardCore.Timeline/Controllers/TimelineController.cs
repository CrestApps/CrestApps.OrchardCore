using System.Security.Claims;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Timeline.Controllers;

/// <summary>
/// Adds and redacts timeline entries from the timeline card of any record page.
/// </summary>
[Admin]
public sealed class TimelineController : Controller
{
    private readonly ITimelineService _timelineService;
    private readonly ITimelineAccessService _accessService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineController"/> class.
    /// </summary>
    /// <param name="timelineService">The timeline service.</param>
    /// <param name="accessService">The timeline access service.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public TimelineController(
        ITimelineService timelineService,
        ITimelineAccessService accessService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<TimelineController> htmlLocalizer)
    {
        _timelineService = timelineService;
        _accessService = accessService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Adds an internal note or a public comment to a record's timeline.
    /// </summary>
    /// <param name="subject">The record, as <c>Kind:Id</c>.</param>
    /// <param name="body">The text.</param>
    /// <param name="isPublic">Whether it is a public comment rather than an internal note.</param>
    /// <param name="returnUrl">The page to go back to.</param>
    /// <returns>A redirect to the page.</returns>
    [HttpPost]
    [Admin("timeline/add", "TimelineAdd")]
    public async Task<IActionResult> Add(string subject, string body, bool isPublic, string returnUrl)
    {
        if (!RecordRef.TryParse(subject, out var record))
        {
            return BadRequest();
        }

        var kind = isPublic ? TimelineConstants.Kinds.Comment : TimelineConstants.Kinds.Note;
        var visibility = isPublic ? TimelineVisibility.Public : TimelineVisibility.Internal;

        if (!await _accessService.CanAddAsync(User, record, kind, visibility, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var result = await _timelineService.AddAsync(new TimelineEntry
        {
            Subject = record,
            Kind = kind,
            Visibility = visibility,
            Author = CurrentUser(),

            // The name stays readable on a site where no party source knows its users.
            AuthorName = User.Identity?.Name,
            Body = body,
        }, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(isPublic ? H["The comment was added."] : H["The note was added."]);
        }
        else
        {
            foreach (var error in result.Errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }
        }

        return Back(returnUrl);
    }

    /// <summary>
    /// Redacts the text of an entry.
    /// </summary>
    /// <param name="id">The entry's id.</param>
    /// <param name="returnUrl">The page to go back to.</param>
    /// <returns>A redirect to the page.</returns>
    [HttpPost]
    [Admin("timeline/redact/{id}", "TimelineRedact")]
    public async Task<IActionResult> Redact(string id, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimelinePermissions.RedactTimelineEntries))
        {
            return Forbid();
        }

        var entry = await _timelineService.FindAsync(id, HttpContext.RequestAborted);

        if (entry is null)
        {
            return NotFound();
        }

        // Redacting needs the right to read the entry too, so a user can't act on records they can't see.
        if (!await _accessService.CanViewAsync(User, entry.Subject, entry.Visibility, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        await _timelineService.RedactAsync(id, CurrentUser(), HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The text of the entry was redacted."]);

        return Back(returnUrl);
    }

    private PartyRef CurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrEmpty(userId) ? default : new PartyRef(PartyKinds.User, userId);
    }

    private IActionResult Back(string returnUrl)
        => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : Redirect("~/Admin");
}
