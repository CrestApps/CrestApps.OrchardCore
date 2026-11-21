using CrestApps.OrchardCore.Learning.Packages.Core.Indexes;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Services;

/// <summary>
/// Stores runtime states in their own collection.
/// </summary>
public sealed class PackageRuntimeStore : DocumentCatalog<PackageRuntimeState, PackageRuntimeStateIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PackageRuntimeStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public PackageRuntimeStore(ISession session)
        : base(session)
    {
        CollectionName = PackagesConstants.CollectionName;
    }

    /// <summary>
    /// Finds the state of an enrollment on a package item.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="packageItemId">The package item.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The state, or <see langword="null"/>.</returns>
    public async Task<PackageRuntimeState> FindAsync(string enrollmentId, string packageItemId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(enrollmentId) || string.IsNullOrEmpty(packageItemId)
            ? null
            : await Session.Query<PackageRuntimeState, PackageRuntimeStateIndex>(index => index.EnrollmentId == enrollmentId && index.PackageItemId == packageItemId, collection: CollectionName)
                .OrderBy(index => index.Id)
                .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Finds the state again from the store, leaving out any copy the session already holds, so a write under the
    /// runtime lock sees what the last writer committed.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="packageItemId">The package item.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The state, or <see langword="null"/>.</returns>
    public async Task<PackageRuntimeState> FindFreshAsync(string enrollmentId, string packageItemId, CancellationToken cancellationToken = default)
    {
        var tracked = await FindAsync(enrollmentId, packageItemId, cancellationToken);

        if (tracked is null)
        {
            return null;
        }

        Session.Detach(tracked, CollectionName);

        return await FindAsync(enrollmentId, packageItemId, cancellationToken);
    }

    /// <summary>
    /// Lists the states of a learner, newest launch first.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The states.</returns>
    public async Task<IReadOnlyList<PackageRuntimeState>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(userId)
            ? []
            : (await Session.Query<PackageRuntimeState, PackageRuntimeStateIndex>(index => index.UserId == userId, collection: CollectionName)
                .OrderByDescending(index => index.LastLaunchedUtc)
                .ThenByDescending(index => index.Id)
                .ListAsync(cancellationToken)).ToArray();

    /// <summary>
    /// Reads a page of states for an export, oldest first.
    /// </summary>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The page.</returns>
    public async Task<IReadOnlyList<PackageRuntimeState>> PageForExportAsync(int skip, int take, CancellationToken cancellationToken = default)
        => (await Session.Query<PackageRuntimeState, PackageRuntimeStateIndex>(collection: CollectionName)
            .OrderBy(index => index.Id)
            .Skip(Math.Max(skip, 0))
            .Take(Math.Max(take, 1))
            .ListAsync(cancellationToken)).ToArray();
}
