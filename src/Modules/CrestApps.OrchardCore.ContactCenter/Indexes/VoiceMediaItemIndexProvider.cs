using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="VoiceMediaItem"/> documents to the <see cref="VoiceMediaItemIndex"/>.
/// </summary>
public sealed class VoiceMediaItemIndexProvider : IndexProvider<VoiceMediaItem>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceMediaItemIndexProvider"/> class.
    /// </summary>
    public VoiceMediaItemIndexProvider()
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<VoiceMediaItem> context)
    {
        context
            .For<VoiceMediaItemIndex>()
            .Map(item => new VoiceMediaItemIndex
            {
                ItemId = item.ItemId,
                Name = item.Name,
            });
    }
}
