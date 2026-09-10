using CrestApps.OrchardCore.Telephony.Core.Indexes;
using CrestApps.OrchardCore.Telephony.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Telephony.Indexes;

/// <summary>
/// Maps <see cref="TelephonyExtension"/> documents to the <see cref="TelephonyExtensionIndex"/>.
/// </summary>
public sealed class TelephonyExtensionIndexProvider : IndexProvider<TelephonyExtension>
{
    /// <inheritdoc/>
    public override void Describe(DescribeContext<TelephonyExtension> context)
    {
        context
            .For<TelephonyExtensionIndex>()
            .Map(extension => new TelephonyExtensionIndex
            {
                ItemId = extension.ItemId,
                Name = extension.Name,
                Number = TelephonyExtension.NormalizeNumber(extension.Number),
                UserId = extension.UserId,
            });
    }
}
