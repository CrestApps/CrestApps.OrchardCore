using CrestApps.OrchardCore.WorkQueues;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.YesSql.Core.WorkQueues;

/// <summary>
/// Maps a work item type to the <see cref="LeasedWorkItemIndex"/> of its collection.
/// </summary>
/// <typeparam name="T">The work item type.</typeparam>
public sealed class LeasedWorkItemIndexProvider<T> : IndexProvider<T>
    where T : class, ILeasedWorkItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LeasedWorkItemIndexProvider{T}"/> class.
    /// </summary>
    /// <param name="collectionName">The collection the items live in, or <see langword="null"/> for the default one.</param>
    public LeasedWorkItemIndexProvider(string collectionName)
    {
        CollectionName = collectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<T> context)
    {
        context
            .For<LeasedWorkItemIndex>()
            .Map(item => new LeasedWorkItemIndex
            {
                ItemId = item.ItemId,
                Status = item.Status,
                NextAttemptUtc = item.NextAttemptUtc,
            });
    }
}
