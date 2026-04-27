using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Data.YesSql.Indexes.AI;
using CrestApps.Core.Data.YesSql.Services;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Represents the default AI profile store.
/// </summary>
public sealed class DefaultAIProfileStore : NamedDocumentCatalog<AIProfile, AIProfileIndex>, IAIProfileStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAIProfileStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public DefaultAIProfileStore(ISession session)
    : base(session, AIConstants.AICollectionName)
    {
    }

    /// <summary>
    /// Retrieves the by type async.
    /// </summary>
    /// <param name="type">The type.</param>
    public async ValueTask<IReadOnlyCollection<AIProfile>> GetByTypeAsync(AIProfileType type)
    {
        var items = await Session.Query<AIProfile, AIProfileIndex>(collection: CollectionName).ListAsync();

        return items.Where(profile => profile.Type == type).ToArray();
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
        return ValueTask.CompletedTask;
    }
}
