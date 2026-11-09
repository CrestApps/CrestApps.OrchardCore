using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using Microsoft.Extensions.Options;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Reads the chat session analytics events from the AI collection, narrowed by the session metrics index.
/// </summary>
public sealed class YesSqlAIChatSessionEventReader : IAIChatSessionEventReader
{
    private readonly ISession _session;
    private readonly YesSqlStoreOptions _storeOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="YesSqlAIChatSessionEventReader"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="storeOptions">The YesSql store options, which name the AI collection.</param>
    public YesSqlAIChatSessionEventReader(ISession session, IOptions<YesSqlStoreOptions> storeOptions)
    {
        _session = session;
        _storeOptions = storeOptions.Value;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AIChatSessionEvent>> ListAsync(AIChatSessionEventQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var events = _session.Query<AIChatSessionEvent, AIChatSessionMetricsIndex>(collection: _storeOptions.AICollectionName);

        if (!string.IsNullOrEmpty(query.ProfileId))
        {
            var profileId = query.ProfileId;
            events = events.Where(index => index.ProfileId == profileId);
        }

        if (query.FromUtc.HasValue)
        {
            var fromUtc = query.FromUtc.Value;
            events = events.Where(index => index.SessionStartedUtc >= fromUtc);
        }

        if (query.ToUtc.HasValue)
        {
            var toUtc = query.ToUtc.Value;
            events = events.Where(index => index.SessionStartedUtc <= toUtc);
        }

        return (await events.OrderByDescending(index => index.SessionStartedUtc).ListAsync(cancellationToken)).ToList();
    }
}
