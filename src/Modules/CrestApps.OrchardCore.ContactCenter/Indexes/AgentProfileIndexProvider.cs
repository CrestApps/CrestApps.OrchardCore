using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="AgentProfile"/> documents to the <see cref="AgentProfileIndex"/>.
/// </summary>
public sealed class AgentProfileIndexProvider : IndexProvider<AgentProfile>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentProfileIndexProvider"/> class.
    /// </summary>
    public AgentProfileIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AgentProfile> context)
    {
        context
            .For<AgentProfileIndex>()
            .Map(profile => new AgentProfileIndex
            {
                ItemId = profile.ItemId,
                Name = profile.Name,
                UserId = profile.UserId,
                PresenceStatus = profile.PresenceStatus,
            });
    }
}
