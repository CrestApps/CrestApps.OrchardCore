using CrestApps.OrchardCore.AI.Chat.Indexes;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.AI.Chat.Services;

public sealed class CustomChatInstanceCatalog : SourceDocumentCatalog<AICustomChatInstance, AICustomChatInstanceIndex>, ICustomChatInstanceCatalog
{
    public CustomChatInstanceCatalog(ISession session)
        : base(session)
    {
        CollectionName = AICustomChatConstants.CollectionName;
    }

    public async ValueTask<IReadOnlyCollection<AICustomChatInstance>> GetByUserAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return (await Session.Query<AICustomChatInstance, AICustomChatInstanceIndex>(
            x => x.UserId == userId,
            collection: CollectionName)
            .OrderByDescending(x => x.CreatedUtc)
            .ListAsync()).ToArray();
    }

    public async ValueTask<AICustomChatInstance> FindByIdForUserAsync(string itemId, string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(itemId);
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return await Session.Query<AICustomChatInstance, AICustomChatInstanceIndex>(
            x => x.ItemId == itemId && x.UserId == userId,
            collection: CollectionName)
            .FirstOrDefaultAsync();
    }
}
