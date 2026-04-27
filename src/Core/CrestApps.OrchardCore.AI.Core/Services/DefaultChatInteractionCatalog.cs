using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql.Indexes.ChatInteractions;
using CrestApps.Core.Data.YesSql.Services;
using CrestApps.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Represents the default chat interaction catalog.
/// </summary>
public sealed class DefaultChatInteractionCatalog : DocumentCatalog<ChatInteraction, ChatInteractionIndex>, ICatalog<ChatInteraction>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultChatInteractionCatalog"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public DefaultChatInteractionCatalog(ISession session)
    : base(session, AIConstants.AICollectionName)
    {
    }

    protected override ValueTask PagingAsync<TQuery>(IQuery<ChatInteraction> query, TQuery context)
    {
        if (context is ChatInteractionQueryContext c && !string.IsNullOrEmpty(c.UserId))
        {
            query = query.With<ChatInteractionIndex>(x => x.UserId == c.UserId);
        }

        if (!string.IsNullOrWhiteSpace(context.Name))
        {
            query = query.With<ChatInteractionIndex>(x => x.Title != null && x.Title.Contains(context.Name));
        }

        if (context.Sorted)
        {
            query = query.With<ChatInteractionIndex>()
                .OrderByDescending(x => x.CreatedUtc)
                .ThenBy(x => x.Id);
        }
        else
        {
            query = query.With<ChatInteractionIndex>()
                .OrderBy(x => x.Id);
        }

        return ValueTask.CompletedTask;
    }
}
