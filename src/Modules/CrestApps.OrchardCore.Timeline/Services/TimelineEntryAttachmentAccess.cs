using System.Security.Claims;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Services;

namespace CrestApps.OrchardCore.Timeline.Services;

/// <summary>
/// The files of a timeline entry are as visible as the entry: whoever may read the entry, on its record and with its
/// visibility, may download them, and whoever may add such an entry may change them.
/// </summary>
internal sealed class TimelineEntryAttachmentAccess : IAttachmentAccessProvider
{
    private readonly ITimelineService _timelineService;
    private readonly ITimelineAccessService _accessService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineEntryAttachmentAccess"/> class.
    /// </summary>
    /// <param name="timelineService">The timeline service.</param>
    /// <param name="accessService">The timeline access service.</param>
    public TimelineEntryAttachmentAccess(ITimelineService timelineService, ITimelineAccessService accessService)
    {
        _timelineService = timelineService;
        _accessService = accessService;
    }

    /// <inheritdoc/>
    public string OwnerKind => TimelineConstants.AttachmentOwnerKind;

    /// <inheritdoc/>
    public async Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef owner, CancellationToken cancellationToken = default)
    {
        var entry = await _timelineService.FindAsync(owner.Id, cancellationToken);

        return entry is not null
            && entry.RedactedUtc is null
            && await _accessService.CanViewAsync(user, entry.Subject, entry.Visibility, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> CanManageAsync(ClaimsPrincipal user, RecordRef owner, CancellationToken cancellationToken = default)
    {
        var entry = await _timelineService.FindAsync(owner.Id, cancellationToken);

        return entry is not null
            && await _accessService.CanAddAsync(user, entry.Subject, entry.Kind, entry.Visibility, cancellationToken);
    }
}
