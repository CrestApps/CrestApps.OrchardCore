using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer.Indexes;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Keeps the draft and the versions of designed reports. The builder saves changes into the draft as people work;
/// publishing makes the draft the report that runs and keeps a version when something changed; a version can be
/// restored into the draft. Every change states the revision it was based on and is refused when someone else changed
/// the report since, unless the person chooses to overwrite. Changes to one report are serialized with a lock, so the
/// revision check holds on several nodes.
/// </summary>
public sealed class ReportDesignHistoryService
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly ReportDesignService _designService;
    private readonly ReportDesignHistoryStore _store;
    private readonly IDistributedLock _distributedLock;
    private readonly IReportDesignNotifier _notifier;
    private readonly ReportDesignVersionOptions _options;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDesignHistoryService"/> class.
    /// </summary>
    /// <param name="designService">The design service that stores reports.</param>
    /// <param name="store">The store of drafts and versions.</param>
    /// <param name="distributedLock">The lock that serializes changes to one report.</param>
    /// <param name="notifier">The notifier that tells the other editors.</param>
    /// <param name="options">The version retention options.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public ReportDesignHistoryService(
        ReportDesignService designService,
        ReportDesignHistoryStore store,
        IDistributedLock distributedLock,
        IReportDesignNotifier notifier,
        IOptions<ReportDesignVersionOptions> options,
        IClock clock,
        ILogger<ReportDesignHistoryService> logger)
    {
        _designService = designService;
        _store = store;
        _distributedLock = distributedLock;
        _notifier = notifier;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Gets what the builder edits for a report: its draft when it has unpublished changes, otherwise the report.
    /// </summary>
    /// <param name="design">The published report.</param>
    /// <returns>The working copy.</returns>
    public async Task<ReportDesignWorkingCopy> GetWorkingCopyAsync(ReportDesign design)
    {
        ArgumentNullException.ThrowIfNull(design);

        var draft = await _store.FindDraftAsync(design.ItemId);
        var hasDraft = draft?.HasChanges == true && draft.Design is not null;

        return new ReportDesignWorkingCopy
        {
            Design = hasDraft ? Snapshot(design, draft.Design) : design,
            Revision = draft?.Revision ?? 0,
            HasDraft = hasDraft,
            ModifiedByName = hasDraft ? draft.ModifiedByName : null,
            ModifiedUtc = hasDraft ? draft.ModifiedUtc : null,
        };
    }

    /// <summary>
    /// Gets what the builder edits for a report that was never published: its draft.
    /// </summary>
    /// <param name="draft">The draft of the unpublished report.</param>
    /// <returns>The working copy.</returns>
    public static ReportDesignWorkingCopy GetWorkingCopy(ReportDesignDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return new ReportDesignWorkingCopy
        {
            Design = draft.Design,
            Revision = draft.Revision,
            HasDraft = true,
            IsUnpublished = true,
            ModifiedByName = draft.ModifiedByName,
            ModifiedUtc = draft.ModifiedUtc,
        };
    }

    /// <summary>
    /// Starts a new report as a draft, so the work on it is saved from the first change. It gets its identifier now and
    /// keeps it when it is first published; until then only its owner and the people who manage every report see it.
    /// </summary>
    /// <param name="incoming">The report as the builder has it.</param>
    /// <param name="user">The person designing it, who becomes its owner.</param>
    /// <returns>The outcome, with <see cref="ReportHistoryResult.DesignId"/> set.</returns>
    public async Task<ReportHistoryResult> CreateDraftAsync(ReportDesign incoming, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(user);

        var owner = new ReportDesign
        {
            ItemId = IdGenerator.GenerateId(),
            OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier),
            Author = user.Identity?.Name,
            CreatedUtc = _clock.UtcNow,
        };
        var draft = new ReportDesignDraft
        {
            DesignId = owner.ItemId,
            IsUnpublished = true,
            HasChanges = true,
            Design = Snapshot(owner, incoming),
        };

        Touch(draft, user);
        await _store.SaveDraftAsync(draft);
        await _store.CommitAsync();

        return Result(ReportHistoryStatus.Saved, draft, null, null);
    }

    /// <summary>
    /// Finds the draft of a report that was never published.
    /// </summary>
    /// <param name="designId">The report identifier.</param>
    /// <returns>The draft, or <see langword="null"/> when there is no such report.</returns>
    public async Task<ReportDesignDraft> FindUnpublishedAsync(string designId)
    {
        if (string.IsNullOrEmpty(designId))
        {
            return null;
        }

        var draft = await _store.FindDraftAsync(designId);

        return draft is { IsUnpublished: true, Design: not null } ? draft : null;
    }

    /// <summary>
    /// Lists the drafts of the reports that were never published.
    /// </summary>
    /// <returns>The drafts.</returns>
    public async Task<IReadOnlyList<ReportDesignDraft>> ListUnpublishedAsync()
    {
        return (await _store.ListUnpublishedAsync())
            .Where(draft => draft.Design is not null)
            .ToArray();
    }

    /// <summary>
    /// Deletes a report that was never published.
    /// </summary>
    /// <param name="designId">The report identifier.</param>
    /// <param name="revision">The revision the person last saw.</param>
    /// <param name="force">Whether to delete even when someone else changed it since.</param>
    /// <param name="user">The person deleting it.</param>
    /// <returns>The outcome.</returns>
    public async Task<ReportHistoryResult> DeleteUnpublishedAsync(string designId, long revision, bool force, ClaimsPrincipal user)
    {
        ArgumentException.ThrowIfNullOrEmpty(designId);
        ArgumentNullException.ThrowIfNull(user);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(LockKey(designId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return new ReportHistoryResult { Status = ReportHistoryStatus.Busy };
        }

        await using (locker)
        {
            var draft = await FindUnpublishedAsync(designId);

            if (draft is null)
            {
                return new ReportHistoryResult { Status = ReportHistoryStatus.NotFound };
            }

            if (!force && draft.Revision != revision)
            {
                return Result(ReportHistoryStatus.Conflict, draft, null, null);
            }

            _store.DeleteDraft(draft);
            await _store.CommitAsync();
            await _notifier.ReportDesignChangedAsync(new ReportDesignChange
            {
                Kind = ReportDesignChangeKind.Deleted,
                DesignId = designId,
                UserId = user.FindFirstValue(ClaimTypes.NameIdentifier),
                UserName = user.Identity?.Name,
            });

            return new ReportHistoryResult { Status = ReportHistoryStatus.Saved, DesignId = designId };
        }
    }

    /// <summary>
    /// Saves changes into the draft of a report.
    /// </summary>
    /// <param name="design">The published report.</param>
    /// <param name="incoming">The report as the builder has it.</param>
    /// <param name="revision">The revision the changes are based on.</param>
    /// <param name="force">Whether to save even when someone else changed the report since.</param>
    /// <param name="user">The person saving.</param>
    /// <returns>The outcome.</returns>
    public Task<ReportHistoryResult> SaveDraftAsync(ReportDesign design, ReportDesign incoming, long revision, bool force, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(user);

        return ChangeDraftAsync(design.ItemId, revision, force, ReportDesignChangeKind.DraftSaved, user, draft =>
        {
            draft.Design = Snapshot(design, incoming);
            draft.HasChanges = true;

            return Task.FromResult<int?>(null);
        });
    }

    /// <summary>
    /// Throws away the unpublished changes of a report.
    /// </summary>
    /// <param name="design">The published report.</param>
    /// <param name="revision">The revision the person last saw.</param>
    /// <param name="force">Whether to discard even when someone else changed the draft since.</param>
    /// <param name="user">The person discarding.</param>
    /// <returns>The outcome.</returns>
    public Task<ReportHistoryResult> DiscardDraftAsync(ReportDesign design, long revision, bool force, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(user);

        return ChangeDraftAsync(design.ItemId, revision, force, ReportDesignChangeKind.DraftDiscarded, user, draft =>
        {
            draft.Design = null;
            draft.HasChanges = false;
            draft.RestoredFrom = null;

            return Task.FromResult<int?>(null);
        });
    }

    /// <summary>
    /// Copies a version into the draft of a report, to be checked and published.
    /// </summary>
    /// <param name="design">The published report.</param>
    /// <param name="number">The version number.</param>
    /// <param name="revision">The revision the person last saw.</param>
    /// <param name="force">Whether to restore even when someone else changed the report since.</param>
    /// <param name="user">The person restoring.</param>
    /// <returns>The outcome, which is <see cref="ReportHistoryStatus.NotFound"/> when the version does not exist.</returns>
    public async Task<ReportHistoryResult> RestoreAsync(ReportDesign design, int number, long revision, bool force, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(user);

        var version = await _store.FindVersionAsync(design.ItemId, number);

        if (version?.Design is null)
        {
            return new ReportHistoryResult { Status = ReportHistoryStatus.NotFound };
        }

        return await ChangeDraftAsync(design.ItemId, revision, force, ReportDesignChangeKind.Restored, user, draft =>
        {
            draft.Design = Snapshot(design, version.Design);
            draft.HasChanges = true;
            draft.RestoredFrom = number;

            return Task.FromResult<int?>(number);
        });
    }

    /// <summary>
    /// Publishes a report: stores it as the report that runs, keeps a version when it changed, and clears the draft.
    /// A new report is created; when <paramref name="incoming"/> carries the identifier of a report that exists only as
    /// a draft, that report is created with it.
    /// </summary>
    /// <param name="incoming">The report as the builder has it.</param>
    /// <param name="existing">The published report, or <see langword="null"/> for a new or unpublished one.</param>
    /// <param name="revision">The revision the changes are based on.</param>
    /// <param name="force">Whether to publish even when someone else changed the report since.</param>
    /// <param name="user">The person publishing.</param>
    /// <param name="canSharePublicly">Whether the person may share reports publicly.</param>
    /// <returns>The outcome; <see cref="ReportHistoryResult.Save"/> holds the save problems.</returns>
    public async Task<ReportHistoryResult> PublishAsync(ReportDesign incoming, ReportDesign existing, long revision, bool force, ClaimsPrincipal user, bool canSharePublicly)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(user);

        if (existing is null && string.IsNullOrEmpty(incoming.ItemId))
        {
            var created = await _designService.SaveAsync(incoming, null, user, canSharePublicly);

            if (!created.Saved)
            {
                return new ReportHistoryResult { Status = ReportHistoryStatus.Invalid, Save = created };
            }

            var draft = new ReportDesignDraft { DesignId = created.Id };
            var number = await RecordVersionAsync(created.Id, null, created.Design, null, user);

            Touch(draft, user);
            await _store.SaveDraftAsync(draft);
            await NotifyAsync(ReportDesignChangeKind.Published, draft, number, user);

            return Result(ReportHistoryStatus.Saved, draft, number, created);
        }

        var designId = existing?.ItemId ?? incoming.ItemId;

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(LockKey(designId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return new ReportHistoryResult { Status = ReportHistoryStatus.Busy };
        }

        await using (locker)
        {
            var draft = await _store.FindDraftAsync(designId);

            if (existing is null && draft is not { IsUnpublished: true })
            {
                return new ReportHistoryResult { Status = ReportHistoryStatus.NotFound };
            }

            draft ??= new ReportDesignDraft { DesignId = designId };

            if (!force && draft.Revision != revision)
            {
                return Result(ReportHistoryStatus.Conflict, draft, null, null);
            }

            var before = existing?.Clone();

            incoming.ItemId = designId;

            var saved = await _designService.SaveAsync(incoming, existing, user, canSharePublicly);

            if (!saved.Saved)
            {
                return new ReportHistoryResult { Status = ReportHistoryStatus.Invalid, Revision = draft.Revision, DesignId = designId, Save = saved };
            }

            var number = await RecordVersionAsync(designId, before, saved.Design, draft.RestoredFrom, user);

            draft.Design = null;
            draft.HasChanges = false;
            draft.IsUnpublished = false;
            draft.RestoredFrom = null;
            Touch(draft, user);
            await _store.SaveDraftAsync(draft);
            await _store.CommitAsync();
            await NotifyAsync(ReportDesignChangeKind.Published, draft, number, user);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("User '{UserName}' published designed report '{ReportId}' at revision {Revision}.", user.Identity?.Name, designId, draft.Revision);
            }

            return Result(ReportHistoryStatus.Saved, draft, number, saved);
        }
    }

    /// <summary>
    /// Lists the versions of a report from the newest.
    /// </summary>
    /// <param name="designId">The report identifier.</param>
    /// <returns>The versions.</returns>
    public Task<IReadOnlyList<ReportDesignVersionIndex>> ListVersionsAsync(string designId)
    {
        return _store.ListVersionsAsync(designId);
    }

    /// <summary>
    /// Finds a version of a report.
    /// </summary>
    /// <param name="designId">The report identifier.</param>
    /// <param name="number">The version number.</param>
    /// <returns>The version, or <see langword="null"/>.</returns>
    public Task<ReportDesignVersion> FindVersionAsync(string designId, int number)
    {
        return _store.FindVersionAsync(designId, number);
    }

    /// <summary>
    /// Deletes a report with its share links, draft, and versions.
    /// </summary>
    /// <param name="design">The report.</param>
    /// <param name="user">The person deleting it.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task DeleteAsync(ReportDesign design, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(design);

        await _designService.DeleteAsync(design);
        await _store.DeleteAllAsync(design.ItemId);
        await _notifier.ReportDesignChangedAsync(new ReportDesignChange
        {
            Kind = ReportDesignChangeKind.Deleted,
            DesignId = design.ItemId,
            UserId = user?.FindFirstValue(ClaimTypes.NameIdentifier),
            UserName = user?.Identity?.Name,
        });
    }

    /// <summary>
    /// Determines whether two reports differ in anything people see or that changes what the report reads.
    /// </summary>
    /// <param name="left">One report.</param>
    /// <param name="right">The other report.</param>
    /// <returns><see langword="true"/> when they are the same.</returns>
    public static bool HasSameContent(ReportDesign left, ReportDesign right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return JsonSerializer.Serialize(Content(left), ReportDesignerJson.Options) == JsonSerializer.Serialize(Content(right), ReportDesignerJson.Options);
    }

    private async Task<ReportHistoryResult> ChangeDraftAsync(string designId, long revision, bool force, ReportDesignChangeKind kind, ClaimsPrincipal user, Func<ReportDesignDraft, Task<int?>> change)
    {
        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(LockKey(designId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return new ReportHistoryResult { Status = ReportHistoryStatus.Busy };
        }

        await using (locker)
        {
            var draft = await _store.FindDraftAsync(designId) ?? new ReportDesignDraft { DesignId = designId };

            if (!force && draft.Revision != revision)
            {
                return Result(ReportHistoryStatus.Conflict, draft, null, null);
            }

            var number = await change(draft);

            Touch(draft, user);
            await _store.SaveDraftAsync(draft);
            await _store.CommitAsync();
            await NotifyAsync(kind, draft, number, user);

            return Result(ReportHistoryStatus.Saved, draft, number, null);
        }
    }

    // Adds a version for a newly published report when it differs from the latest one. A report published before
    // versions existed first gets its previous state as version 1, so it can be restored.
    private async Task<int?> RecordVersionAsync(string designId, ReportDesign before, ReportDesign after, int? restoredFrom, ClaimsPrincipal user)
    {
        var latest = await _store.FindLatestVersionAsync(designId);

        if (latest is null && before is not null && !HasSameContent(before, after))
        {
            latest = new ReportDesignVersion
            {
                DesignId = designId,
                Number = 1,
                Design = before.Clone(),
                CreatedUtc = before.ModifiedUtc ?? before.CreatedUtc,
                CreatedByName = before.Author,
            };

            await _store.AddVersionAsync(latest);
        }

        if (latest is not null && HasSameContent(latest.Design, after))
        {
            return null;
        }

        var version = new ReportDesignVersion
        {
            DesignId = designId,
            Number = (latest?.Number ?? 0) + 1,
            Design = after.Clone(),
            CreatedUtc = _clock.UtcNow,
            CreatedById = user.FindFirstValue(ClaimTypes.NameIdentifier),
            CreatedByName = user.Identity?.Name,
            RestoredFrom = restoredFrom,
        };

        await _store.AddVersionAsync(version);

        if (_options.MaxVersions > 0)
        {
            await _store.TrimVersionsAsync(designId, _options.MaxVersions);
        }

        return version.Number;
    }

    private void Touch(ReportDesignDraft draft, ClaimsPrincipal user)
    {
        draft.Revision++;
        draft.ModifiedUtc = _clock.UtcNow;
        draft.ModifiedById = user.FindFirstValue(ClaimTypes.NameIdentifier);
        draft.ModifiedByName = user.Identity?.Name;
    }

    private Task NotifyAsync(ReportDesignChangeKind kind, ReportDesignDraft draft, int? versionNumber, ClaimsPrincipal user)
    {
        return _notifier.ReportDesignChangedAsync(new ReportDesignChange
        {
            Kind = kind,
            DesignId = draft.DesignId,
            Revision = draft.Revision,
            VersionNumber = versionNumber,
            UserId = draft.ModifiedById,
            UserName = user.Identity?.Name,
        });
    }

    private static ReportHistoryResult Result(ReportHistoryStatus status, ReportDesignDraft draft, int? versionNumber, ReportSaveResult save)
    {
        return new ReportHistoryResult
        {
            Status = status,
            DesignId = draft.DesignId,
            Revision = draft.Revision,
            ModifiedByName = draft.ModifiedByName,
            ModifiedUtc = draft.ModifiedUtc,
            VersionNumber = versionNumber,
            Save = save,
        };
    }

    // The report as the builder edits it: the editable parts of the incoming copy, normalized, on top of the stored
    // report's identity and ownership, which the builder never changes.
    private static ReportDesign Snapshot(ReportDesign published, ReportDesign content)
    {
        var snapshot = published.Clone();

        snapshot.DisplayText = ReportDesignNormalizer.Truncate(content.DisplayText);
        snapshot.Description = content.Description?.Trim();
        snapshot.Category = ReportDesignNormalizer.Truncate(content.Category);
        snapshot.Query = ReportDesignNormalizer.Normalize(content.Query);
        snapshot.Visuals = ReportDesignNormalizer.Normalize(content.Visuals);
        snapshot.ShowInAdminMenu = content.ShowInAdminMenu;
        snapshot.AllowExport = content.AllowExport;
        snapshot.SharedUserNames = (content.SharedUserNames ?? []).Where(name => !string.IsNullOrWhiteSpace(name)).Take(500).ToList();
        snapshot.SharedRoles = (content.SharedRoles ?? []).Where(role => !string.IsNullOrWhiteSpace(role)).Take(500).ToList();

        return snapshot;
    }

    private static object Content(ReportDesign design)
    {
        return new
        {
            design.DisplayText,
            design.Description,
            design.Category,
            design.Query,
            design.Visuals,
            design.ShowInAdminMenu,
            design.AllowExport,
            design.SharedUserNames,
            design.SharedRoles,
        };
    }

    private static string LockKey(string designId)
    {
        return "CrestApps.Reports.Design:" + designId;
    }
}

/// <summary>
/// What the builder edits for a report.
/// </summary>
public sealed class ReportDesignWorkingCopy
{
    /// <summary>
    /// Gets or sets the report: the draft when it has unpublished changes, otherwise the published report.
    /// </summary>
    public ReportDesign Design { get; set; }

    /// <summary>
    /// Gets or sets the revision the builder bases its changes on.
    /// </summary>
    public long Revision { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the report has unpublished changes.
    /// </summary>
    public bool HasDraft { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the report was never published.
    /// </summary>
    public bool IsUnpublished { get; set; }

    /// <summary>
    /// Gets or sets who last changed the draft.
    /// </summary>
    public string ModifiedByName { get; set; }

    /// <summary>
    /// Gets or sets when the draft last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}

/// <summary>
/// The outcome of a change to a report's draft or versions.
/// </summary>
public enum ReportHistoryStatus
{
    /// <summary>
    /// The change was saved.
    /// </summary>
    Saved,

    /// <summary>
    /// Someone else changed the report since the revision the change was based on.
    /// </summary>
    Conflict,

    /// <summary>
    /// Another change to the report was in progress for too long.
    /// </summary>
    Busy,

    /// <summary>
    /// The report could not be published; <see cref="ReportHistoryResult.Save"/> says why.
    /// </summary>
    Invalid,

    /// <summary>
    /// The version does not exist.
    /// </summary>
    NotFound,
}

/// <summary>
/// The outcome of a change to a report's draft or versions.
/// </summary>
public sealed class ReportHistoryResult
{
    /// <summary>
    /// Gets or sets the outcome.
    /// </summary>
    public ReportHistoryStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the report.
    /// </summary>
    public string DesignId { get; set; }

    /// <summary>
    /// Gets or sets the revision of the report after the change, or its current revision on a conflict.
    /// </summary>
    public long Revision { get; set; }

    /// <summary>
    /// Gets or sets who last changed the report.
    /// </summary>
    public string ModifiedByName { get; set; }

    /// <summary>
    /// Gets or sets when the report last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the version the change published or restored, if any.
    /// </summary>
    public int? VersionNumber { get; set; }

    /// <summary>
    /// Gets or sets the outcome of saving the report, when it was published.
    /// </summary>
    public ReportSaveResult Save { get; set; }
}

/// <summary>
/// How many versions of each report are kept.
/// </summary>
public sealed class ReportDesignVersionOptions
{
    /// <summary>
    /// Gets or sets how many versions of each report to keep; the oldest are deleted. Zero or less keeps them all.
    /// </summary>
    public int MaxVersions { get; set; } = 50;
}
