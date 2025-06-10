using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Documents;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using YesSql;

namespace CrestApps.OrchardCore.AI.Migrations;

[Obsolete("This class will be removed before the v1 is released.")]
internal sealed class ProfileStoreMigrations : DataMigration
{
    private readonly ICatalog<AIProfile> _profilesCatalog;
    private readonly IDocumentManager<AIProfileDocument> _profileDocument;

    public ProfileStoreMigrations(
        ICatalog<AIProfile> profilesCatalog,
        IDocumentManager<AIProfileDocument> profileDocument)
    {
        _profilesCatalog = profilesCatalog;
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

    public int UpdateFrom1()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var store = scope.ServiceProvider.GetRequiredService<IStore>();
            var shellSettings = scope.ServiceProvider.GetRequiredService<ShellSettings>();
            var dbConnectionAccessor = scope.ServiceProvider.GetRequiredService<IDbConnectionAccessor>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ProfileStoreMigrations>>();
            var dialect = store.Configuration.SqlDialect;

            var table = $"{store.Configuration.TablePrefix}{shellSettings.GetDocumentTable()}";

            var quotedTypeColumnName = dialect.QuoteForColumnName("Type");

            var command = $"update {dialect.QuoteForTableName(table, store.Configuration.Schema)} set {quotedTypeColumnName} = replace({quotedTypeColumnName}, 'CrestApps.OrchardCore.Models.ModelDocument', 'CrestApps.OrchardCore.Models.DictionaryDocument') where {quotedTypeColumnName} like 'CrestApps.OrchardCore.Models.ModelDocument%';";

            await using var connection = dbConnectionAccessor.CreateConnection();

            try
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync(command);
                await connection.CloseAsync();
            }
            catch (Exception e)
            {
                logger.LogError(e, "An error occurred while updating indexing tasks Category to Content.");

                throw;
            }
        });

        return 2;
    }
}
