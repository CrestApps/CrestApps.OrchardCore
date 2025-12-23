using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultChatInteractionCatalog : SourceDocumentCatalog<ChatInteraction, ChatInteractionIndex>, ISourceCatalog<ChatInteraction>
{
    public DefaultChatInteractionCatalog(ISession session)
        : base(session)
    {
    }

    protected override ValueTask PagingAsync<TQuery>(IQuery<ChatInteraction> query, TQuery context)
    {
        if (context is ChatInteractionQueryContext c && !string.IsNullOrEmpty(c.UserId))
        {
            query = query.With<ChatInteractionIndex>(x => x.UserId == c.UserId);
        }

        return ValueTask.CompletedTask;
    }
}
