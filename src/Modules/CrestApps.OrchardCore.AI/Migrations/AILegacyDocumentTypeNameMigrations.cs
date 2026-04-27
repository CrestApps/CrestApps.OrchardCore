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
    private const string CurrentAssemblyName = "CrestApps.Core.AI.Abstractions";

    private static readonly (string LegacyNamespacePrefix, string LegacyAssemblyName, string CurrentNamespacePrefix)[] _legacyTypeNameReplacements =
    [
        ("CrestApps.OrchardCore.AI.Models.", "CrestApps.OrchardCore.AI.Abstractions", "CrestApps.Core.AI.Models."),
        ("CrestApps.AI.", "CrestApps.AI.Abstractions", "CrestApps.Core.AI."),
    ];

    /// <summary>
    /// Creates a new .
    /// </summary>
    public static int Create()
    {
        ShellScope.AddDeferredTask(scope => RewriteLegacyTypeNamesAsync(scope.ServiceProvider));

        return 3;
    }

    /// <summary>
    /// Updates the from1.
    /// </summary>
    public static int UpdateFrom1()
    {
        ShellScope.AddDeferredTask(scope => RewriteLegacyTypeNamesAsync(scope.ServiceProvider));

        return 3;
    }

    /// <summary>
    /// Updates the from2.
    /// </summary>
    public static int UpdateFrom2()
    {
        ShellScope.AddDeferredTask(scope => RewriteLegacyTypeNamesAsync(scope.ServiceProvider));

        return 3;
    }

    private static async Task RewriteLegacyTypeNamesAsync(IServiceProvider scope)
    {
        var store = scope.GetRequiredService<IStore>();
        var dbConnectionAccessor = scope.GetRequiredService<IDbConnectionAccessor>();
        var logger = scope.GetRequiredService<ILogger<AILegacyDocumentTypeNameMigrations>>();

        var dialect = store.Configuration.SqlDialect;
        var documentTableName = store.Configuration.TableNameConvention.GetDocumentTable(AIConstants.AICollectionName);
        var table = $"{store.Configuration.TablePrefix}{documentTableName}";
        var quotedTableName = dialect.QuoteForTableName(table, store.Configuration.Schema);
        var quotedTypeColumnName = dialect.QuoteForColumnName(nameof(Document.Type));

        await using var connection = dbConnectionAccessor.CreateConnection();
        await connection.OpenAsync();

        var totalUpdated = 0;

        foreach (var (legacyNamespacePrefix, legacyAssemblyName, currentNamespacePrefix) in _legacyTypeNameReplacements)
        {
            var whereClause =
                $"{quotedTypeColumnName} LIKE '{legacyNamespacePrefix}%' AND {quotedTypeColumnName} LIKE '%, {legacyAssemblyName}%'";

            var count = await connection.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM {quotedTableName} WHERE {whereClause}");

            if (count == 0)
            {
                continue;
            }

            totalUpdated += await connection.ExecuteAsync(
                $"""
                UPDATE {quotedTableName}

                SET {quotedTypeColumnName} = REPLACE(
                REPLACE({quotedTypeColumnName}, '{legacyNamespacePrefix}', '{currentNamespacePrefix}'),
                '{legacyAssemblyName}',
                '{CurrentAssemblyName}')

                WHERE {whereClause}
                """);
        }

        if (totalUpdated > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Updated {Count} legacy AI document type names in {TableName} to the current CrestApps.Core assemblies.",
                totalUpdated,
                table);
        }
    }
}
