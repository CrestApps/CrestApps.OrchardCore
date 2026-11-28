using CrestApps.OrchardCore.Geo.Core.Indexes;
using CrestApps.OrchardCore.Geo.Core.Services;
using CrestApps.OrchardCore.Geo.Models;
using YesSql;

namespace CrestApps.OrchardCore.Geo.Services;

/// <summary>
/// What the territory editor, the territory list and the settings page do beyond the territory manager: merging members
/// by kind, moving a deleted territory's children up, and counting the geocoding queue. The admin pages and the AI tools
/// both call it.
/// </summary>
internal static class TerritoryCommands
{
    /// <summary>
    /// Replaces the members of some kinds and keeps the others as they were.
    /// </summary>
    /// <param name="existing">The territory's members.</param>
    /// <param name="kindsToReplace">The kinds whose members are replaced.</param>
    /// <param name="idsByKind">The new members' ids by kind; a kind to replace that is missing ends with no members.</param>
    /// <returns>The members.</returns>
    public static List<TerritoryMember> MergeMembers(IEnumerable<TerritoryMember> existing, IReadOnlyCollection<string> kindsToReplace, IReadOnlyDictionary<string, List<string>> idsByKind)
    {
        ArgumentNullException.ThrowIfNull(kindsToReplace);

        var kinds = kindsToReplace.ToHashSet(StringComparer.Ordinal);

        return
        [
            .. (existing ?? []).Where(member => !kinds.Contains(member.Kind ?? string.Empty)),
            .. kinds.SelectMany(kind => (idsByKind?.GetValueOrDefault(kind) ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Select(id => new TerritoryMember { Kind = kind, Id = id, Role = TerritoryRole.Primary })),
        ];
    }

    /// <summary>
    /// Deletes a territory, after moving its child territories up to its parent.
    /// </summary>
    /// <param name="manager">The territory manager.</param>
    /// <param name="territory">The territory.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The children that moved.</returns>
    public static async Task<IReadOnlyList<ServiceTerritory>> DeleteAsync(ServiceTerritoryManager manager, ServiceTerritory territory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(territory);

        var children = (await manager.GetAllAsync(cancellationToken)).Where(other => other.ParentId == territory.ItemId).ToList();

        foreach (var child in children)
        {
            child.ParentId = territory.ParentId;
            await manager.UpdateAsync(child, cancellationToken: cancellationToken);
        }

        await manager.DeleteAsync(territory, cancellationToken);

        return children;
    }

    /// <summary>
    /// Counts the background geocoding requests by state, and finds the last error of one that is waiting or failed.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <returns>The counts and the last error.</returns>
    public static async Task<GeocodeQueueStatus> GetQueueStatusAsync(ISession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var requests = await session.QueryIndex<GeocodeRequestIndex>(collection: GeoConstants.CollectionName).ListAsync();
        var status = new GeocodeQueueStatus
        {
            Waiting = requests.Count(index => index.Status == nameof(GeocodeRequestStatus.Pending)),
            Done = requests.Count(index => index.Status == nameof(GeocodeRequestStatus.Done)),
            Failed = requests.Count(index => index.Status == nameof(GeocodeRequestStatus.Failed)),
        };

        if (status.Waiting + status.Failed > 0)
        {
            var done = nameof(GeocodeRequestStatus.Done);
            var troubled = await session.Query<GeocodeRequest, GeocodeRequestIndex>(index => index.Status != done, collection: GeoConstants.CollectionName)
                .OrderByDescending(index => index.NextAttemptUtc)
                .Take(20)
                .ListAsync();

            status.LastError = troubled.Select(request => request.LastError).FirstOrDefault(error => !string.IsNullOrEmpty(error));
        }

        return status;
    }
}

/// <summary>
/// The state of the background geocoding queue.
/// </summary>
internal sealed class GeocodeQueueStatus
{
    /// <summary>
    /// Gets or sets how many requests wait for an attempt.
    /// </summary>
    public int Waiting { get; set; }

    /// <summary>
    /// Gets or sets how many requests were geocoded.
    /// </summary>
    public int Done { get; set; }

    /// <summary>
    /// Gets or sets how many requests were given up.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Gets or sets the last error of a request that waits or failed, or <see langword="null"/>.
    /// </summary>
    public string LastError { get; set; }
}
