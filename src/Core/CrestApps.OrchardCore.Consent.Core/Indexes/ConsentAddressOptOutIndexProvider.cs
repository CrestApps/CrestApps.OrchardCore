using CrestApps.OrchardCore.Consent.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Consent.Core.Indexes;

/// <summary>
/// Maps each <see cref="ConsentAddressOptOut"/> to one <see cref="ConsentAddressOptOutIndex"/> row.
/// </summary>
public sealed class ConsentAddressOptOutIndexProvider : IndexProvider<ConsentAddressOptOut>
{
    /// <inheritdoc/>
    public override void Describe(DescribeContext<ConsentAddressOptOut> context)
    {
        context
            .For<ConsentAddressOptOutIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(optOut => new ConsentAddressOptOutIndex
            {
                ItemId = optOut.ItemId,
                Channel = optOut.Channel,
                Address = optOut.Address,
                Purpose = optOut.Purpose,
            });
    }
}
