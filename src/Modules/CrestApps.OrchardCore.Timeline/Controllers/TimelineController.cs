using System.Security.Claims;
using CrestApps.Core;
using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly TimelineOptions _options;
    private readonly ILogger _logger;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineController"/> class.
    /// </summary>
    /// <param name="timelineService">The timeline service.</param>
    /// <param name="accessService">The timeline access service.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="options">The timeline options, for the kinds the composer offers.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public TimelineController(
        ITimelineService timelineService,
        ITimelineAccessService accessService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IOptions<TimelineOptions> options,
        ILogger<TimelineController> logger,
        IHtmlLocalizer<TimelineController> htmlLocalizer)
    {
        _options = options.Value;
        _logger = logger;
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
    /// <param name="kind">The kind of entry, one of the kinds the composer offers.</param>
    /// <param name="returnUrl">The page to go back to.</param>
    /// <param name="files">The files to attach, when the Attachments feature is enabled.</param>
    /// <returns>A redirect to the page.</returns>
    [HttpPost]
    [Admin("timeline/add", "TimelineAdd")]
    public async Task<IActionResult> Add(string subject, string body, string kind, string returnUrl, List<IFormFile> files)
    {
        // Only the kinds the composer offers can be posted here; a module posts its own kinds through its own action.
        var offered = _options.ComposerKinds.FirstOrDefault(option => string.Equals(option.Kind, kind, StringComparison.Ordinal));

        if (!RecordRef.TryParse(subject, out var record) || offered is null
            || (offered.SubjectKinds.Count > 0 && !offered.SubjectKinds.Contains(record.Kind)))
        {
            return BadRequest();
        }

        var cancellationToken = HttpContext.RequestAborted;
        var visibility = offered.Visibility;

        if (!await _accessService.CanAddAsync(User, record, offered.Kind, visibility, cancellationToken))
        {
            return Forbid();
        }

        var entry = new TimelineEntry
        {
            ItemId = UniqueId.GenerateId(),
            Subject = record,
            Kind = offered.Kind,
            Visibility = visibility,
            Author = CurrentUser(),

            // The name stays readable on a site where no party source knows its users.
            AuthorName = User.Identity?.Name,
            Body = body,
        };

        // The files belong to the entry, so they are exactly as visible as the entry.
        var attachments = HttpContext.RequestServices.GetService<IAttachmentService>();
        var uploads = files?.Where(file => file is { Length: > 0 }).ToList() ?? [];
        var attached = new List<string>();

        if (uploads.Count > 0 && attachments is null)
        {
            await _notifier.ErrorAsync(H["Files can't be attached here."]);

            return Refused(record, body, returnUrl);
        }

        try
        {
            if (uploads.Count > 0)
            {
                var owner = new RecordRef(TimelineConstants.AttachmentOwnerKind, entry.ItemId);
                var stored = await attachments.AddAsync(await ReadAsync(uploads, owner, entry.Author, cancellationToken), cancellationToken);

                if (!stored.Succeeded)
                {
                    foreach (var error in stored.Errors)
                    {
                        await _notifier.ErrorAsync(H["{0}", error]);
                    }

                    return Refused(record, body, returnUrl);
                }

                attached.AddRange(stored.Attachments.Select(attachment => attachment.ItemId));
                entry.AttachmentIds = [.. attached];
            }

            var result = await _timelineService.AddAsync(entry, cancellationToken);

            if (!result.Succeeded)
            {
                await DeleteFilesAsync(attachments, attached);

                foreach (var error in result.Errors)
                {
                    await _notifier.ErrorAsync(H["{0}", error]);
                }

                return Refused(record, body, returnUrl);
            }
        }
        catch
        {
            // Whatever went wrong, the files written for this entry don't outlive it.
            await DeleteFilesAsync(attachments, attached);

            throw;
        }

        await _notifier.SuccessAsync(visibility == TimelineVisibility.Public ? H["The comment was added."] : H["The note was added."]);

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

        // A redacted entry keeps no files either: what was written and what was attached go together.
        if (entry.AttachmentIds.Count > 0 && HttpContext.RequestServices.GetService<IAttachmentService>() is { } attachments)
        {
            foreach (var attachmentId in entry.AttachmentIds)
            {
                await attachments.DeleteAsync(attachmentId, HttpContext.RequestAborted);
            }
        }

        await _notifier.SuccessAsync(H["The entry was redacted."]);

        return Back(returnUrl);
    }

    // Goes back with what was typed kept, so a refused file or entry doesn't lose the text.
    private IActionResult Refused(RecordRef record, string body, string returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            TempData[ViewComponents.TimelineViewComponent.DraftKey] = record + "\n" + body;
        }

        return Back(returnUrl);
    }

    // Best effort: a cleanup that fails must not hide why the post failed.
    private async Task DeleteFilesAsync(IAttachmentService attachments, List<string> attachmentIds)
    {
        foreach (var id in attachmentIds)
        {
            try
            {
                await attachments.DeleteAsync(id, CancellationToken.None);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "A file attached to a timeline entry that wasn't stored couldn't be deleted.");
            }
        }
    }

    private static async Task<List<AttachmentUpload>> ReadAsync(List<IFormFile> files, RecordRef owner, PartyRef uploadedBy, CancellationToken cancellationToken)
    {
        var uploads = new List<AttachmentUpload>(files.Count);

        foreach (var file in files)
        {
            using var buffer = new MemoryStream();

            await file.CopyToAsync(buffer, cancellationToken);

            uploads.Add(new AttachmentUpload
            {
                Owner = owner,
                FileName = file.FileName,
                DeclaredContentType = file.ContentType,
                Content = buffer.ToArray(),
                UploadedBy = uploadedBy,
            });
        }

        return uploads;
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
