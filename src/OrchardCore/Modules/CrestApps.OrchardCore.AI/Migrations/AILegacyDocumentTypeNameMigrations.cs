using CrestApps.OrchardCore.AI.Core;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// Rewrites legacy Orchard-layer AI document type names to the current framework-layer types.
/// This keeps existing tenant data readable after switching between branches that stored AI
/// documents under different assemblies.
/// </summary>
internal sealed class AILegacyDocumentTypeNameMigrations : DataMigration
{
    private const string LegacyNamespacePrefix = "CrestApps.OrchardCore.AI.Models.";
    private const string LegacyAssemblyName = "CrestApps.OrchardCore.AI.Abstractions";
    private const string CurrentNamespacePrefix = "CrestApps.AI.Models.";
    private const string CurrentAssemblyName = "CrestApps.AI.Abstractions";
    public static int Create()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var store = scope.ServiceProvider.GetRequiredService<IStore>();
            var dbConnectionAccessor = scope.ServiceProvider.GetRequiredService<IDbConnectionAccessor>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AILegacyDocumentTypeNameMigrations>>();
            var dialect = store.Configuration.SqlDialect;
            var documentTableName = store.Configuration.TableNameConvention.GetDocumentTable(AIConstants.AICollectionName);
            var table = $"{store.Configuration.TablePrefix}{documentTableName}";
            var quotedTableName = dialect.QuoteForTableName(table, store.Configuration.Schema);
            var quotedTypeColumnName = dialect.QuoteForColumnName(nameof(Document.Type));
            var whereClause =
                $"{quotedTypeColumnName} LIKE '{LegacyNamespacePrefix}%' AND {quotedTypeColumnName} LIKE '%, {LegacyAssemblyName}%'";
            await using var connection = dbConnectionAccessor.CreateConnection();
            await connection.OpenAsync();
            var count = await connection.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM {quotedTableName} WHERE {whereClause}");

            if (count == 0)
            {
                return;
            }

            var updated = await connection.ExecuteAsync(
                $"""

                UPDATE {quotedTableName}

                SET {quotedTypeColumnName} = REPLACE(

                REPLACE({quotedTypeColumnName}, '{LegacyNamespacePrefix}', '{CurrentNamespacePrefix}'),

                '{LegacyAssemblyName}',

                '{CurrentAssemblyName}')

                WHERE {whereClause}

                """);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Updated {Count} legacy AI document type names in {TableName} from Orchard-layer assemblies to framework-layer assemblies.",
                    updated,
                    table);
            }
        });

        return 1;
    }
}
