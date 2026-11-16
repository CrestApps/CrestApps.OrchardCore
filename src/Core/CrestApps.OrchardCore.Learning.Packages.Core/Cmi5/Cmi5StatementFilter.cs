using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.Xapi;
using CrestApps.OrchardCore.Learning.Xapi.Core;
using CrestApps.OrchardCore.Learning.Xapi.Core.Services;
using CrestApps.OrchardCore.Locking;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Cmi5;

/// <summary>
/// Enforces the cmi5 rules on the statements of cmi5 registrations, inside the LRS write. A statement is checked when it
/// is sent with a cmi5 launch token, or when it has the cmi5 category and names a registration of a cmi5 package; a
/// statement that breaks a rule refuses the whole request with 403. The runtime lock of each registration's state is
/// held from the check to the end, so the order rules see every statement of the registration; once the statements are
/// stored, the state keeps what they reported, <c>satisfied</c> is written for what is newly satisfied, and the package
/// item's result goes to Learning.
/// </summary>
public sealed class Cmi5StatementFilter : IXapiStatementFilter
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(60);

    private readonly PackageRuntimeStore _store;
    private readonly IPackageContentSource _contentSource;
    private readonly IEnrollmentStore _enrollments;
    private readonly Cmi5Progress _progress;
    private readonly XapiHomePage _homePage;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="Cmi5StatementFilter"/> class.
    /// </summary>
    public Cmi5StatementFilter(
        PackageRuntimeStore store,
        IPackageContentSource contentSource,
        IEnrollmentStore enrollments,
        Cmi5Progress progress,
        XapiHomePage homePage,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock)
    {
        _store = store;
        _contentSource = contentSource;
        _enrollments = enrollments;
        _progress = progress;
        _homePage = homePage;
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task OnWritingAsync(XapiStatementWriteContext context, Func<Task> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var launch = context.Credential?.Launch;
        var isCmi5Launch = string.Equals(launch?.Kind, Cmi5Vocabulary.LaunchKind, StringComparison.Ordinal);
        var targets = new Dictionary<string, (string EnrollmentId, string PackageItemId)>(StringComparer.Ordinal);

        foreach (var statement in context.Statements)
        {
            var registration = XapiAgents.Text(statement["context"]?["registration"])?.ToLowerInvariant();

            if (string.IsNullOrEmpty(registration) || targets.ContainsKey(registration) || (!isCmi5Launch && !Cmi5Rules.IsCmi5Defined(statement)))
            {
                continue;
            }

            if (isCmi5Launch && registration == launch.Registration)
            {
                targets[registration] = (launch.Properties["enrollmentId"], launch.Properties["packageItemId"]);

                continue;
            }

            var found = await _session.QueryIndex<Cmi5RegistrationIndex>(index => index.Registration == registration, collection: PackagesConstants.CollectionName).FirstOrDefaultAsync();

            if (found is not null)
            {
                targets[registration] = (found.EnrollmentId, found.PackageItemId);
            }
        }

        if (targets.Count == 0)
        {
            await next();

            return;
        }

        await using var locker = await _distributedLock.LockAllAsync(targets.Values.Select(target => PackageRuntimeService.LockKey(target.EnrollmentId, target.PackageItemId)), _lockTimeout, _lockExpiration);

        if (locker is null)
        {
            context.Reject(503, "The learner's progress is being saved. Try again.");

            return;
        }

        var now = _clock.UtcNow;
        var states = new Dictionary<string, (PackageRuntimeState State, LearningPackagePart Part)>(StringComparer.Ordinal);

        foreach (var (registration, target) in targets)
        {
            var state = await _store.FindFreshAsync(target.EnrollmentId, target.PackageItemId);
            var item = await _contentSource.GetPublishedAsync(target.PackageItemId);
            var part = item is not null && item.TryGet<LearningPackagePart>(out var found) ? found : null;

            if (state?.Cmi5 is null || part?.Cmi5 is null || !string.Equals(state.Cmi5.Registration, registration, StringComparison.Ordinal))
            {
                context.Reject(403, "The registration is not a cmi5 registration of this site.");

                return;
            }

            states[registration] = (state, part);
        }

        // Checked in order on the state, so a statement sees the ones before it in the request.
        foreach (var statement in context.Statements)
        {
            var registration = XapiAgents.Text(statement["context"]?["registration"])?.ToLowerInvariant();

            if (registration is null || !states.TryGetValue(registration, out var entry) || context.ExistingIds.Contains(XapiAgents.Text(statement["id"]) ?? string.Empty))
            {
                continue;
            }

            var error = Cmi5Rules.CheckAndApply(statement, entry.State.Cmi5, entry.Part.Cmi5, now);

            if (error is not null)
            {
                context.Reject(403, error);

                return;
            }
        }

        await next();

        if (context.RejectedStatus is not null || context.Stored.Count == 0)
        {
            return;
        }

        var homePage = await _homePage.GetAsync();

        foreach (var (state, part) in states.Values)
        {
            // Storing the statements committed the session: the state is read again, under the same lock, and takes
            // what the statements reported.
            var fresh = await _store.FindFreshAsync(state.EnrollmentId, state.PackageItemId);

            fresh.Cmi5 = state.Cmi5;

            var enrollment = await _enrollments.FindByIdAsync(fresh.EnrollmentId);
            var actor = XapiAgents.Account(homePage, fresh.UserId);

            await _progress.EvaluateAsync(fresh, part, enrollment, actor, homePage, now, recordProgress: enrollment?.Status == EnrollmentStatus.Active);
        }

        await _session.SaveChangesAsync();
    }
}
