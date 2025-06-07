using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Dapper;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Documents;
using OrchardCore.Environment.Shell;
using YesSql;

namespace CrestApps.OrchardCore.AI.Migrations;

[Obsolete("This class will be removed before the v1 is released.")]
internal sealed class ProfileStoreMigrations : DataMigration
{
    private readonly INamedCatalog<AIProfile> _profilesCatalog;
    private readonly IStore _store;
    private readonly IDbConnectionAccessor _dbConnectionAccessor;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;
    private readonly IDocumentManager<AIProfileDocument> _profileDocument;

    public ProfileStoreMigrations(
        INamedCatalog<AIProfile> profilesCatalog,
        IStore store,
        IDbConnectionAccessor dbConnectionAccessor,
        ShellSettings shellSettings,
        ILogger<ProfileStoreMigrations> logger,
        IDocumentManager<AIProfileDocument> profileDocument)
    {
        _profilesCatalog = profilesCatalog;
        _store = store;
        _dbConnectionAccessor = dbConnectionAccessor;
        _shellSettings = shellSettings;
        _logger = logger;
        _profileDocument = profileDocument;
    }

    public async Task<int> CreateAsync()
    {
        var profilesDocument = await _profileDocument.GetOrCreateImmutableAsync();

        foreach (var profile in profilesDocument.Profiles.Values)
        {
            try
            {
                await _profilesCatalog.UpdateAsync(profile);
                await _profilesCatalog.SaveChangesAsync();
            }
            catch { }
        }

        return 2;
    }

    public async Task<int> UpdateFrom1Async()
    {
        var dialect = _store.Configuration.SqlDialect;

        var table = $"{_store.Configuration.TablePrefix}{_shellSettings.GetDocumentTable()}";

        var quotedTypeColumnName = dialect.QuoteForColumnName("Type");

        var command = $"update {dialect.QuoteForTableName(table, _store.Configuration.Schema)} set {quotedTypeColumnName} = replace({quotedTypeColumnName}, 'CrestApps.OrchardCore.Models.ModelDocument', 'CrestApps.OrchardCore.Models.DictionaryDocument') where {quotedTypeColumnName} like 'CrestApps.OrchardCore.Models.ModelDocument%';";

        await using var connection = _dbConnectionAccessor.CreateConnection();

        try
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync(command);
            await connection.CloseAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred while updating indexing tasks Category to Content.");

            throw;
        }

        return 2;
    }
}
