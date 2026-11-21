using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using Dapper;
using Microsoft.Extensions.Caching.Memory;
using OrchardCore.Environment.Shell;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Results;

/// <summary>
/// Reads the definition of an immutable version for the answer index. It reads the document with a plain statement on
/// the session's own transaction, never through a session query, so it can run while the session maps index rows.
/// Definitions are cached in memory by tenant and version id; a version never changes.
/// </summary>
public sealed class SurveyVersionDefinitionReader
{
    private static readonly TimeSpan _slidingExpiration = TimeSpan.FromMinutes(30);

    private readonly ISession _session;
    private readonly IMemoryCache _memoryCache;
    private readonly string _tenant;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyVersionDefinitionReader"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="memoryCache">The memory cache.</param>
    /// <param name="shellSettings">The tenant settings.</param>
    public SurveyVersionDefinitionReader(
        ISession session,
        IMemoryCache memoryCache,
        ShellSettings shellSettings)
    {
        _session = session;
        _memoryCache = memoryCache;
        _tenant = shellSettings.Name;
    }

    /// <summary>
    /// Gets the definition of a version.
    /// </summary>
    /// <param name="versionId">The version.</param>
    /// <returns>The definition, or <see langword="null"/>.</returns>
    public async Task<SurveyDefinition> GetAsync(string versionId)
    {
        if (string.IsNullOrEmpty(versionId))
        {
            return null;
        }

        var key = $"Surveys:IndexDefinition:{_tenant}:{versionId}";

        if (_memoryCache.TryGetValue(key, out SurveyDefinition cached))
        {
            return cached;
        }

        var configuration = _session.Store.Configuration;
        var dialect = configuration.SqlDialect;
        var documents = dialect.QuoteForTableName(configuration.TablePrefix + configuration.TableNameConvention.GetDocumentTable(SurveysConstants.CollectionName), configuration.Schema);
        var versions = dialect.QuoteForTableName(configuration.TablePrefix + configuration.TableNameConvention.GetIndexTable(typeof(SurveyVersionIndex), SurveysConstants.CollectionName), configuration.Schema);
        var sql = $"SELECT d.{dialect.QuoteForColumnName("Content")} FROM {documents} d INNER JOIN {versions} v ON v.{dialect.QuoteForColumnName("DocumentId")} = d.{dialect.QuoteForColumnName("Id")} WHERE v.{dialect.QuoteForColumnName("ItemId")} = @ItemId";

        var transaction = await _session.BeginTransactionAsync();
        var content = await transaction.Connection.QueryFirstOrDefaultAsync<string>(new CommandDefinition(sql, new { ItemId = versionId }, transaction));

        if (string.IsNullOrEmpty(content))
        {
            return null;
        }

        var version = (SurveyVersion)configuration.ContentSerializer.Deserialize(content, typeof(SurveyVersion));
        var definition = version?.Definition;

        if (definition is not null)
        {
            _memoryCache.Set(key, definition, new MemoryCacheEntryOptions { SlidingExpiration = _slidingExpiration });
        }

        return definition;
    }
}
