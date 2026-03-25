using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIProfileStore : NamedDocumentCatalog<AIProfile, AIProfileIndex>, IAIProfileStore
{
    public DefaultAIProfileStore(ISession session)
        : base(session)
    {
        CollectionName = AIConstants.AICollectionName;
    }

    public async ValueTask<IReadOnlyCollection<AIProfile>> GetByTypeAsync(AIProfileType type)
    {
        var typeValue = type.ToString();

        var items = await Session.Query<AIProfile, AIProfileIndex>(
            x => x.Type == typeValue,
            collection: CollectionName)
            .ListAsync();

        return items.ToArray();
    }

    protected override ValueTask DeletingAsync(AIProfile entry)
    {
        var settings = entry.GetSettings<AIProfileSettings>();

        if (!settings.IsRemovable)
        {
            throw new InvalidOperationException("The profile cannot be removed.");
        }

        return ValueTask.CompletedTask;
    }

    protected override ValueTask PagingAsync<TQuery>(IQuery<AIProfile> query, TQuery context)
    {
        if (context is AIProfileQueryContext { IsListableOnly: true })
        {
            query.With<AIProfileIndex>(x => x.IsListable);
        }

        return ValueTask.CompletedTask;
    }
}
