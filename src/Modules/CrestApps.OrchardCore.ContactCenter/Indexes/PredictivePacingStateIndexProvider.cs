using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="PredictivePacingState"/> documents to the <see cref="PredictivePacingStateIndex"/>.
/// </summary>
public sealed class PredictivePacingStateIndexProvider : IndexProvider<PredictivePacingState>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PredictivePacingStateIndexProvider"/> class.
    /// </summary>
    public PredictivePacingStateIndexProvider()
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<PredictivePacingState> context)
    {
        context
            .For<PredictivePacingStateIndex>()
            .Map(state => new PredictivePacingStateIndex
            {
                ItemId = state.ItemId,
                QueueId = state.QueueId,
            });
    }
}
