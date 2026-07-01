using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="CallbackRequest"/> documents to the <see cref="CallbackRequestIndex"/>.
/// </summary>
public sealed class CallbackRequestIndexProvider : IndexProvider<CallbackRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackRequestIndexProvider"/> class.
    /// </summary>
    public CallbackRequestIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<CallbackRequest> context)
    {
        context
            .For<CallbackRequestIndex>()
            .Map(callback => new CallbackRequestIndex
            {
                ItemId = callback.ItemId,
                Status = callback.Status,
                ScheduledUtc = callback.ScheduledUtc,
            });
    }
}
