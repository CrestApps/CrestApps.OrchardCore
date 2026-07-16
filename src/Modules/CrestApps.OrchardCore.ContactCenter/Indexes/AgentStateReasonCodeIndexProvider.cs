using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="AgentStateReasonCode"/> documents to the <see cref="AgentStateReasonCodeIndex"/>.
/// </summary>
public sealed class AgentStateReasonCodeIndexProvider : IndexProvider<AgentStateReasonCode>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateReasonCodeIndexProvider"/> class.
    /// </summary>
    public AgentStateReasonCodeIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AgentStateReasonCode> context)
    {
        context
            .For<AgentStateReasonCodeIndex>()
            .Map(reasonCode => new AgentStateReasonCodeIndex
            {
                ItemId = reasonCode.ItemId,
                Name = reasonCode.Name,
                SortOrder = reasonCode.SortOrder,
                Enabled = reasonCode.Enabled,
            });
    }
}
