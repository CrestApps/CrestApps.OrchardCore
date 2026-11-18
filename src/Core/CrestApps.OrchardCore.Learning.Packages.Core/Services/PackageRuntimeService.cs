using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Core.Archives;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Services;

/// <summary>
/// Launches packages and keeps their runtime state. A launch checks the learner's own enrollment, access window and
/// the item's release rules, starts a session and returns a signed launch token. A runtime call (sent by the player page
/// with the learner's session) must carry the token of the latest launch; every value is replayed through the SCORM 1.2 or SCORM 2004
/// data model on the server, so a value the browser accepted is never trusted. Writes to one runtime state run under
/// one lock per enrollment and package item.
/// </summary>
public sealed class PackageRuntimeService
{
    /// <summary>
    /// The most values one runtime call may carry.
    /// </summary>
    public const int MaxChangesPerRequest = 1000;

    private static readonly FileExtensionContentTypeProvider _contentTypes = new();

    private readonly PackageRuntimeStore _store;
    private readonly IPackageContentSource _contentSource;
    private readonly ICourseStructureService _courseStructure;
    private readonly CourseItemLearnerResolver _learnerResolver;
    private readonly CourseReleaseEvaluator _release;
    private readonly IEnrollmentStore _enrollments;
    private readonly ILearnerProgressService _progress;
    private readonly IProtectedFileStore<LearningPackageFiles> _files;
    private readonly PackageLaunchTokens _tokens;
    private readonly EnrollmentWriteGate _writeGate;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageRuntimeService"/> class.
    /// </summary>
    public PackageRuntimeService(
        PackageRuntimeStore store,
        IPackageContentSource contentSource,
        ICourseStructureService courseStructure,
        CourseItemLearnerResolver learnerResolver,
        CourseReleaseEvaluator release,
        IEnrollmentStore enrollments,
        ILearnerProgressService progress,
        IProtectedFileStore<LearningPackageFiles> files,
        PackageLaunchTokens tokens,
        EnrollmentWriteGate writeGate,
        IClock clock,
        ILogger<PackageRuntimeService> logger)
    {
        _store = store;
        _contentSource = contentSource;
        _courseStructure = courseStructure;
        _learnerResolver = learnerResolver;
        _release = release;
        _enrollments = enrollments;
        _progress = progress;
        _files = files;
        _tokens = tokens;
        _writeGate = writeGate;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Gets the lock key of a runtime state.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="packageItemId">The package item.</param>
    /// <returns>The key.</returns>
    public static string LockKey(string enrollmentId, string packageItemId)
        => $"Learning:PackageRuntime:{enrollmentId}:{packageItemId}";

    /// <summary>
    /// Launches a package for the signed-in learner.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <param name="studentName">The name the content shows (<c>cmi.core.student_name</c>).</param>
    /// <param name="packageItemId">The package item.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The launch, or why it was refused.</returns>
    public async Task<PackageLaunchResult> LaunchAsync(string userId, string studentName, string packageItemId, CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveLaunchAsync(userId, packageItemId, cancellationToken);

        if (resolved.Error is not null)
        {
            return PackageLaunchResult.Fail(resolved.Error);
        }

        var target = resolved;
        var enrollment = resolved.Enrollment;
        var review = resolved.Review;
        var now = _clock.UtcNow;
        var part = target.Part;
        var launch = new PackageLaunch(IdGenerator.GenerateId(), enrollment.ItemId, target.Item.ContentItemId, part.PackageFileId, userId);
        PackageRuntimeState launched = null;

        var error = await _writeGate.RunLockedAsync(LockKey(enrollment.ItemId, launch.PackageItemId), async _ =>
        {
            var state = await _store.FindFreshAsync(enrollment.ItemId, launch.PackageItemId, cancellationToken);

            // A commit or finish whose progress write found the enrollment busy: its result is recorded now, before the
            // new session can start a new attempt and reset the data it comes from (review round 9). While the
            // enrollment is still busy the launch is refused, so the result is not lost.
            if (state is { ResultNotRecorded: true } && !review)
            {
                var pending = PackagesConstants.Standards.IsScorm2004(state.Standard)
                    ? PackageResultMapper.Map(state.Cmi2004 ?? new Scorm2004Cmi(), launch.PackageItemId, state.Attempts)
                    : PackageResultMapper.Map(state.Cmi ?? new Scorm12Cmi(), launch.PackageItemId, state.Sessions);
                var update = await _progress.RecordWithRetryAsync(enrollment, pending, cancellationToken);

                if (update?.RefusedReason == LearningRefusals.Busy)
                {
                    return PackageRuntimeErrors.Busy;
                }

                // Learning committed the session, which forgets the state read before: read it again.
                state = await _store.FindFreshAsync(enrollment.ItemId, launch.PackageItemId, cancellationToken);
                state.ResultNotRecorded = false;
            }

            var isNew = state is null;
            var standard = string.IsNullOrEmpty(part.Standard) ? PackagesConstants.Standards.Scorm12 : part.Standard;
            var scorm2004 = PackagesConstants.Standards.IsScorm2004(standard);

            state ??= new PackageRuntimeState
            {
                ItemId = IdGenerator.GenerateId(),
                EnrollmentId = enrollment.ItemId,
                UserId = userId,
                CourseId = enrollment.CourseId,
                PackageItemId = launch.PackageItemId,
                Standard = standard,
                Cmi = scorm2004 ? null : new Scorm12Cmi(),
            };

            // A package of the other standard was published into the item: the data of the old one cannot carry over,
            // so the learner starts afresh on the new one. A state written before SCORM 2004 has no standard: SCORM 1.2.
            var switched = !isNew && PackagesConstants.Standards.IsScorm2004(state.Standard) != scorm2004;

            if (switched)
            {
                state.Cmi = scorm2004 ? null : new Scorm12Cmi();
                state.Cmi2004 = null;
            }

            state.Standard = standard;

            var firstSession = state.Sessions == 0 || switched;

            if (scorm2004)
            {
                StartScorm2004(state, part, standard, userId, studentName, review, firstSession);
            }
            else
            {
                StartScorm12(state, part, userId, studentName, review, firstSession);
            }

            if (!review)
            {
                state.Sessions++;
                state.FirstLaunchedUtc ??= now;
                state.LastLaunchedUtc = now;
            }

            state.CurrentLaunchId = launch.LaunchId;
            state.LastSequence = 0;
            state.PackageFileId = part.PackageFileId;
            state.PackageTitle = target.Item.DisplayText;

            if (isNew)
            {
                await _store.CreateAsync(state, cancellationToken);
            }
            else
            {
                await _store.UpdateAsync(state, cancellationToken);
            }

            launched = state;

            return (string)null;
        }, () => PackageRuntimeErrors.Busy, cancellationToken);

        if (error is not null)
        {
            return PackageLaunchResult.Fail(error);
        }

        return new PackageLaunchResult
        {
            Token = _tokens.Create(launch),
            Launch = launch,
            Part = part,
            State = launched,
            Review = review,
        };
    }

    /// <summary>
    /// Finds what a learner's launch of a package item opens: the published item in the outline of a course, the
    /// learner's own enrollment, and whether the launch is a review of a completed enrollment. Otherwise the enrollment
    /// must be active, within the learner's access dates, and the item's release rules must open it, as for any other
    /// item of the course.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <param name="packageItemId">The package item.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The target, or why the launch is refused.</returns>
    public async Task<PackageLaunchTarget> ResolveLaunchAsync(string userId, string packageItemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return PackageLaunchTarget.Fail(PackageRuntimeErrors.NotEnrolled);
        }

        var target = await FindPackageAsync(packageItemId);

        if (target.Error is not null)
        {
            return PackageLaunchTarget.Fail(target.Error);
        }

        var (_, enrollment) = await _learnerResolver.ResolveAsync(userId, target.Item, cancellationToken);

        if (enrollment is null)
        {
            return PackageLaunchTarget.Fail(PackageRuntimeErrors.NotEnrolled);
        }

        // A completed enrollment is a record: the learner may open the package again to review it, and nothing it
        // sends is kept. Otherwise a launch is a learner's write, with the same checks as any other item.
        var review = enrollment.Status == EnrollmentStatus.Completed;

        if (!review)
        {
            var refused = await CheckLearnerAccessAsync(enrollment, target.Entry, _clock.UtcNow, cancellationToken);

            if (refused is not null)
            {
                return PackageLaunchTarget.Fail(refused);
            }
        }

        return new PackageLaunchTarget
        {
            Item = target.Item,
            Part = target.Part,
            Enrollment = enrollment,
            Review = review,
        };
    }

    /// <summary>
    /// Takes the values a package frame set, on the server's data model, for the launch of the token.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was taken and refused.</returns>
    public async Task<PackageRuntimeResult> ApplyAsync(PackageRuntimeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var launch = _tokens.Read(request.Token);

        // The token is the frame's; the session is the learner's. Both must name the same learner, enrollment and item.
        if (launch is null ||
            string.IsNullOrEmpty(request.UserId) ||
            !string.Equals(launch.UserId, request.UserId, StringComparison.Ordinal) ||
            !string.Equals(launch.EnrollmentId, request.EnrollmentId, StringComparison.Ordinal) ||
            !string.Equals(launch.PackageItemId, request.PackageItemId, StringComparison.Ordinal))
        {
            return PackageRuntimeResult.Fail(PackageRuntimeErrors.InvalidToken);
        }

        var changes = request.Changes ?? [];

        if (changes.Count > MaxChangesPerRequest)
        {
            return PackageRuntimeResult.Fail(PackageRuntimeErrors.TooManyChanges);
        }

        var enrollment = await _enrollments.FindByIdAsync(launch.EnrollmentId, cancellationToken);

        if (enrollment is null || !string.Equals(enrollment.UserId, launch.UserId, StringComparison.Ordinal))
        {
            return PackageRuntimeResult.Fail(PackageRuntimeErrors.InvalidToken);
        }

        PackageRuntimeResult result = null;

        var error = await _writeGate.RunLockedAsync(LockKey(launch.EnrollmentId, launch.PackageItemId), async _ =>
        {
            var state = await _store.FindFreshAsync(launch.EnrollmentId, launch.PackageItemId, cancellationToken);

            if (state is null || !string.Equals(state.CurrentLaunchId, launch.LaunchId, StringComparison.Ordinal))
            {
                return PackageRuntimeErrors.Superseded;
            }

            // A call at or below the last one applied was applied already, or is older than one that was: it is answered
            // as taken, and nothing of it is applied (review round 9, M3).
            if (request.Sequence is { } sequence && sequence <= state.LastSequence)
            {
                result = new PackageRuntimeResult { State = state };

                return null;
            }

            var scorm2004 = PackagesConstants.Standards.IsScorm2004(state.Standard);
            var cmi = scorm2004 ? null : state.Cmi ??= new Scorm12Cmi();
            var cmi2004 = scorm2004 ? state.Cmi2004 ??= new Scorm2004Cmi() : null;
            var review = scorm2004
                ? string.Equals(cmi2004.Mode, "review", StringComparison.Ordinal)
                : string.Equals(cmi.LessonMode, "review", StringComparison.Ordinal);

            if (review)
            {
                // A review keeps nothing: the values are checked on a copy, so the frame still gets its errors.
                result = new PackageRuntimeResult { Refused = scorm2004 ? Replay(Copy(cmi2004), changes) : Replay(Copy(cmi), changes), State = state, Review = true };

                return null;
            }

            var now = _clock.UtcNow;

            // A learner's write: the enrollment must still give access, as for any other item.
            if (!EnrollmentAccess.HasAccess(enrollment, now))
            {
                return enrollment.Status == EnrollmentStatus.Active ? PackageRuntimeErrors.AccessEnded : PackageRuntimeErrors.NotActive;
            }

            var refused = scorm2004 ? Replay(cmi2004, changes) : Replay(cmi, changes);

            if (request.Sequence is { } applied)
            {
                state.LastSequence = applied;
            }

            if (request.Finish)
            {
                // Terminate (LMSFinish): the session time joins the total time. SCORM 2004 evaluates its statuses on
                // every read, so it has nothing like the SCORM 1.2 mastery score to apply.
                if (scorm2004)
                {
                    Scorm2004Rules.FinishSession(cmi2004);
                }
                else
                {
                    Scorm12Rules.FinishSession(cmi);
                }

                state.LastFinishedUtc = now;

                // A finished session takes no more values; the next launch starts a new one.
                state.CurrentLaunchId = null;
            }
            else if (request.Commit && !scorm2004)
            {
                Scorm12Rules.ApplyMasteryScore(cmi);
            }

            if (request.Commit || request.Finish)
            {
                state.LastCommitUtc = now;

                // This result replaces one that was not recorded; it is recorded below.
                state.ResultNotRecorded = false;
            }

            // Every runtime call loads and writes the whole state, and every package page embeds it: a change that would
            // make it larger than the limit is refused whole, and nothing of it is saved (review round 9, M4).
            if (System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(state).Length > ScormLimits.MaxStateBytes)
            {
                return PackageRuntimeErrors.StateTooLarge;
            }

            await _store.UpdateAsync(state, cancellationToken);

            result = new PackageRuntimeResult { Refused = refused, State = state };

            // A commit or a finish reports to Learning as the learner's own work, on the learner path: the access window
            // and the item's release rules apply there too, and the result is marked self-reported. Learning commits the
            // session (the state saved above with it), so the state is not touched after this; the runtime lock is held
            // first and the enrollment lock inside it, the order every Learning record write takes.
            if (request.Commit || request.Finish)
            {
                var itemResult = scorm2004
                    ? PackageResultMapper.Map(cmi2004, launch.PackageItemId, state.Attempts)
                    : PackageResultMapper.Map(cmi, launch.PackageItemId, state.Sessions);

                result.Progress = await _progress.RecordWithRetryAsync(enrollment, itemResult, cancellationToken);

                // The enrollment stayed busy, so Learning committed nothing and the state is still the session's: it keeps
                // the flag, and the next launch records the result before it starts a session (review round 9).
                if (result.Progress?.RefusedReason == LearningRefusals.Busy)
                {
                    state.ResultNotRecorded = true;
                    await _store.UpdateAsync(state, cancellationToken);
                }

                if (result.Progress?.RefusedReason is not null && _logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("The package result of item {ItemId} was not recorded on enrollment {EnrollmentId}: {Reason}.", launch.PackageItemId, launch.EnrollmentId, result.Progress.RefusedReason);
                }
            }

            return null;
        }, () => PackageRuntimeErrors.Busy, cancellationToken);

        return error is not null ? PackageRuntimeResult.Fail(error) : result;
    }

    /// <summary>
    /// Opens a file of the package of a launch token.
    /// </summary>
    /// <param name="token">The launch token.</param>
    /// <param name="path">The path of the file in the package.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The file, or <see langword="null"/> when the token is not valid, the enrollment is gone or no longer the
    /// learner's, the package was replaced, or the package has no such file.</returns>
    public async Task<PackageFileResult> OpenFileAsync(string token, string path, CancellationToken cancellationToken = default)
    {
        var launch = _tokens.Read(token);
        var normalized = PackagePath.Normalize(path);

        if (launch is null || normalized is null)
        {
            return null;
        }

        var enrollment = await _enrollments.FindByIdAsync(launch.EnrollmentId, cancellationToken);

        if (enrollment is null ||
            !string.Equals(enrollment.UserId, launch.UserId, StringComparison.Ordinal) ||
            enrollment.Status is not (EnrollmentStatus.Active or EnrollmentStatus.Completed))
        {
            return null;
        }

        // The package of the token only: a package replaced since the launch is not served under the old token.
        var item = await _contentSource.GetPublishedAsync(launch.PackageItemId);
        var part = PackagePart(item);

        if (part is null || !string.Equals(part.PackageFileId, launch.PackageFileId, StringComparison.Ordinal))
        {
            return null;
        }

        var stream = await _files.OpenAsync(launch.PackageFileId, cancellationToken);

        if (stream is null)
        {
            return null;
        }

        ZipArchive archive;

        try
        {
            archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        }
        catch (InvalidDataException exception)
        {
            await stream.DisposeAsync();
            _logger.LogWarning(exception, "The package file {FileId} could not be read.", launch.PackageFileId);

            return null;
        }

        // The stored archive holds only the files that passed the import checks, under their normalized names.
        var entry = archive.GetEntry(normalized)
            ?? archive.Entries.FirstOrDefault(candidate => string.Equals(candidate.FullName, normalized, StringComparison.OrdinalIgnoreCase));

        if (entry is null || entry.FullName.EndsWith('/'))
        {
            archive.Dispose();

            return null;
        }

        if (!_contentTypes.TryGetContentType(entry.FullName, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        var state = contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase)
            ? await _store.FindAsync(launch.EnrollmentId, launch.PackageItemId, cancellationToken)
            : null;

        return new PackageFileResult(entry.Open(), contentType, entry.FullName, new ArchiveOwner(archive))
        {
            Length = entry.Length,
            Launch = launch,
            Standard = part.Standard,
            State = state,
        };
    }

    /// <summary>
    /// Gets the runtime state of an enrollment on a package item.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="packageItemId">The package item.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The state, or <see langword="null"/>.</returns>
    public Task<PackageRuntimeState> GetStateAsync(string enrollmentId, string packageItemId, CancellationToken cancellationToken = default)
        => _store.FindAsync(enrollmentId, packageItemId, cancellationToken);

    private async Task<PackageTarget> FindPackageAsync(string packageItemId)
    {
        var item = await _contentSource.GetPublishedAsync(packageItemId);

        if (PackagePart(item) is null)
        {
            return new PackageTarget(PackageRuntimeErrors.NotAPackage, null, null, null);
        }

        var courseId = _courseStructure.GetCourseId(item);
        var course = string.IsNullOrEmpty(courseId) ? null : await _courseStructure.GetCourseAsync(courseId);
        var entry = course is null ? null : (await _courseStructure.GetOutlineAsync(course)).Find(item.ContentItemId);

        if (entry?.Part is null || !string.Equals(entry.Part.Kind, PackagesConstants.PackageKind, StringComparison.Ordinal))
        {
            return new PackageTarget(PackageRuntimeErrors.NotAPackage, null, null, null);
        }

        var outlineItem = entry.Item ?? item;
        var part = PackagePart(outlineItem) ?? PackagePart(item);

        return string.IsNullOrEmpty(part.PackageFileId)
            ? new PackageTarget(PackageRuntimeErrors.NoPackageFile, null, null, null)
            : new PackageTarget(null, outlineItem, part, entry);
    }

    // As for any other item of the course: an active enrollment, within the learner's access dates, once the release
    // rules open the item. The service has no signed-in user to ask the course access service with, so the enrollment
    // rules are checked here, as coursework does.
    private async Task<string> CheckLearnerAccessAsync(Enrollment enrollment, CourseOutlineEntry entry, DateTime now, CancellationToken cancellationToken)
    {
        if (enrollment.Status != EnrollmentStatus.Active)
        {
            return PackageRuntimeErrors.NotActive;
        }

        if (!EnrollmentAccess.HasAccess(enrollment, now))
        {
            return PackageRuntimeErrors.AccessEnded;
        }

        var progress = await _progress.GetAsync(enrollment.ItemId, cancellationToken);

        return (await _release.EvaluateAsync(entry, enrollment, progress)).IsReleased ? null : PackageRuntimeErrors.NotAvailableYet;
    }

    // A SCORM 1.2 session: the entry from the last exit, and the read-only values from the server at every launch.
    private static void StartScorm12(PackageRuntimeState state, LearningPackagePart part, string userId, string studentName, bool review, bool firstSession)
    {
        var cmi = state.Cmi ??= new Scorm12Cmi();

        if (!review)
        {
            Scorm12Rules.BeginSession(cmi, firstSession);
        }
        else
        {
            cmi.Entry = Scorm12Rules.ReviewEntry(cmi, firstSession);
        }

        cmi.StudentId = userId;
        cmi.StudentName = Truncate(studentName ?? string.Empty, 255);
        cmi.Credit = review ? "no-credit" : "credit";
        cmi.LessonMode = review ? "review" : "normal";
        cmi.MasteryScore = part.MasteryScore?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        cmi.MaxTimeAllowed = part.MaxTimeAllowed ?? string.Empty;
        cmi.TimeLimitAction = part.TimeLimitAction ?? string.Empty;
        cmi.LaunchData = Truncate(part.LaunchData ?? string.Empty, 4096);
    }

    // A SCORM 2004 session: the attempt resumes after a suspend and otherwise starts anew (RTE 4.2.8), and the read-only
    // values come from the server at every launch. A review keeps the attempt as it is.
    private static void StartScorm2004(PackageRuntimeState state, LearningPackagePart part, string standard, string userId, string learnerName, bool review, bool firstSession)
    {
        var cmi = state.Cmi2004 ??= new Scorm2004Cmi();

        cmi.Edition = standard == PackagesConstants.Standards.Scorm2004FourthEdition ? 4 : 3;

        if (review)
        {
            cmi.Entry = Scorm2004Rules.ReviewEntry(cmi, firstSession);
        }
        else if (Scorm2004Rules.BeginSession(cmi, firstSession, part.ObjectiveIds ?? []))
        {
            state.Attempts++;
        }

        cmi.LearnerId = userId;
        cmi.LearnerName = Truncate(learnerName ?? string.Empty, 250);
        cmi.Credit = review ? "no-credit" : "credit";
        cmi.Mode = review ? "review" : "normal";
        cmi.LaunchData = string.IsNullOrEmpty(part.LaunchData) ? null : Truncate(part.LaunchData, 4000);
        cmi.CompletionThreshold = part.CompletionThreshold?.ToString(CultureInfo.InvariantCulture);
        cmi.ScaledPassingScore = part.ScaledPassingScore?.ToString(CultureInfo.InvariantCulture);
        cmi.MaxTimeAllowed = string.IsNullOrEmpty(part.MaxTimeAllowed) ? null : part.MaxTimeAllowed;
        cmi.TimeLimitAction = string.IsNullOrEmpty(part.TimeLimitAction) ? "continue,no message" : part.TimeLimitAction;
    }

    private static List<PackageRuntimeRefusal> Replay(Scorm2004Cmi cmi, IList<PackageRuntimeChange> changes)
    {
        var api = new Scorm2004Api(cmi);
        var refused = new List<PackageRuntimeRefusal>();

        api.Initialize(string.Empty);

        for (var index = 0; index < changes.Count; index++)
        {
            var change = changes[index];

            if (change is null || api.SetValue(change.Element, change.Value) != "true")
            {
                refused.Add(new PackageRuntimeRefusal(index, change?.Element, change is null ? Scorm2004Errors.GeneralArgumentError : api.GetLastError()));
            }
        }

        return refused;
    }

    private static List<PackageRuntimeRefusal> Replay(Scorm12Cmi cmi, IList<PackageRuntimeChange> changes)
    {
        var api = new Scorm12Api(cmi);
        var refused = new List<PackageRuntimeRefusal>();

        api.LMSInitialize(string.Empty);

        for (var index = 0; index < changes.Count; index++)
        {
            var change = changes[index];

            if (change is null || api.LMSSetValue(change.Element, change.Value) != "true")
            {
                refused.Add(new PackageRuntimeRefusal(index, change?.Element, change is null ? Scorm12Errors.InvalidArgument : api.LMSGetLastError()));
            }
        }

        return refused;
    }

    private static T Copy<T>(T cmi)
        => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(cmi));

    private static string Truncate(string value, int length)
        => value.Length <= length ? value : value[..length];

    private static LearningPackagePart PackagePart(ContentItem item)
        => item is not null && item.TryGet<LearningPackagePart>(out var part) ? part : null;

    private sealed record PackageTarget(string Error, ContentItem Item, LearningPackagePart Part, CourseOutlineEntry Entry);

    private sealed class ArchiveOwner : IAsyncDisposable
    {
        private readonly ZipArchive _archive;

        public ArchiveOwner(ZipArchive archive)
        {
            _archive = archive;
        }

        public ValueTask DisposeAsync()
        {
            _archive.Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
