using CrestApps.OrchardCore.AI.Chat.Interactions.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Indexes;

/// <summary>
/// Maps <see cref="ProfileBuilderSession"/> documents to the <see cref="ProfileBuilderSessionIndex"/>.
/// </summary>
internal sealed class ProfileBuilderSessionIndexProvider : IndexProvider<ProfileBuilderSession>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileBuilderSessionIndexProvider"/> class.
    /// </summary>
    public ProfileBuilderSessionIndexProvider()
    {
        CollectionName = ProfileBuilderStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ProfileBuilderSession> context)
    {
        context
            .For<ProfileBuilderSessionIndex>()
            .Map(session => new ProfileBuilderSessionIndex
            {
                ItemId = session.ItemId,
                ChatInteractionId = session.ChatInteractionId,
                OwnerId = session.OwnerId,
                ModifiedUtc = session.ModifiedUtc ?? session.CreatedUtc,
            });
    }
}
