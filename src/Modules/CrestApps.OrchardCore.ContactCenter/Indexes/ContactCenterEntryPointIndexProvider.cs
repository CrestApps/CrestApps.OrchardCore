using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="ContactCenterEntryPoint"/> documents to the <see cref="ContactCenterEntryPointIndex"/>.
/// </summary>
public sealed class ContactCenterEntryPointIndexProvider : IndexProvider<ContactCenterEntryPoint>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEntryPointIndexProvider"/> class.
    /// </summary>
    public ContactCenterEntryPointIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ContactCenterEntryPoint> context)
    {
        context
            .For<ContactCenterEntryPointIndex>()
            .Map(entryPoint => new ContactCenterEntryPointIndex
            {
                ItemId = entryPoint.ItemId,
                Name = entryPoint.Name,
                Enabled = entryPoint.Enabled,
            });
    }
}
