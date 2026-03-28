using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// Migrates existing AIProfile data from the legacy single-document DictionaryDocument storage
/// into individual YesSql documents stored in the AI collection. This ensures each profile is
/// stored as a separate row, avoiding SQL Server's nvarchar(max) size limitations and improving
/// scalability.
/// </summary>
internal sealed class AIProfileDocumentMigrations : DataMigration
{
    private const int _batchSize = 50;

    // Match the DictionaryDocument<AIProfile> type name pattern used by YesSql.
    private const string _dictionaryDocumentTypePrefix =
        "CrestApps.OrchardCore.Models.DictionaryDocument`1[[CrestApps.OrchardCore.AI.Models.AIProfile, CrestApps.OrchardCore.AI.Abstractions, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null]], CrestApps.OrchardCore.Abstractions";

    public static int Create()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var store = scope.ServiceProvider.GetRequiredService<IStore>();
            var dbConnectionAccessor = scope.ServiceProvider.GetRequiredService<IDbConnectionAccessor>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AIProfileDocumentMigrations>>();

            var dialect = store.Configuration.SqlDialect;

            // The DictionaryDocument<AIProfile> is stored in the default collection (empty string).
            var documentTableName = store.Configuration.TableNameConvention.GetDocumentTable(string.Empty);
            var table = $"{store.Configuration.TablePrefix}{documentTableName}";
            var quotedTableName = dialect.QuoteForTableName(table, store.Configuration.Schema);

            var quotedIdColumnName = dialect.QuoteForColumnName(nameof(Document.Id));
            var quotedTypeColumnName = dialect.QuoteForColumnName(nameof(Document.Type));
            var quotedContentColumnName = dialect.QuoteForColumnName(nameof(Document.Content));

            await using var connection = dbConnectionAccessor.CreateConnection();
            await connection.OpenAsync();

            // Query for the DictionaryDocument<AIProfile> in the default Document table.
            var sqlBuilder = new SqlBuilder(store.Configuration.TablePrefix, store.Configuration.SqlDialect);
            sqlBuilder.AddSelector(quotedIdColumnName);
            sqlBuilder.AddSelector("," + quotedContentColumnName);
            sqlBuilder.From(quotedTableName);
            sqlBuilder.WhereAnd($" {quotedTypeColumnName} = '{_dictionaryDocumentTypePrefix}' ");

            try
            {
                var documents = (await connection.QueryAsync<Document>(sqlBuilder.ToSqlString())).ToList();

                if (documents.Count == 0)
                {
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug("No DictionaryDocument<AIProfile> found. Skipping migration.");
                    }

                    return;
                }

                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Found {Count} DictionaryDocument(s) containing AIProfile records to migrate.", documents.Count);
                }

                var totalMigrated = 0;

                foreach (var document in documents)
                {
                    var jsonObject = JsonNode.Parse(document.Content);

                    if (jsonObject?["Records"] is not JsonObject recordsObject || recordsObject.Count == 0)
                    {
                        continue;
                    }

                    var profiles = new List<AIProfile>();

                    foreach (var record in recordsObject)
                    {
                        if (record.Value is not JsonObject profileObject)
                        {
                            continue;
                        }

                        try
                        {
                            var profile = profileObject.Deserialize<AIProfile>(JOptions.Default);

                            if (profile is not null)
                            {
                                // Ensure the ItemId is set (use the dictionary key as fallback).
                                if (string.IsNullOrEmpty(profile.ItemId))
                                {
                                    profile.ItemId = record.Key;
                                }

                                profiles.Add(profile);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to deserialize AIProfile with key '{Key}'. Skipping.", record.Key);
                        }
                    }

                    if (profiles.Count == 0)
                    {
                        continue;
                    }

                    // Save each profile as an individual document in the AI collection, in batches.
                    for (var batchStart = 0; batchStart < profiles.Count; batchStart += _batchSize)
                    {
                        var batch = profiles.Skip(batchStart).Take(_batchSize).ToList();

                        using var session = store.CreateSession();

                        foreach (var profile in batch)
                        {
                            await session.SaveAsync(profile, collection: AIConstants.AICollectionName);
                        }

                        await session.SaveChangesAsync();

                        if (logger.IsEnabled(LogLevel.Information))
                        {
                            logger.LogInformation(
                                "Migrated AIProfile batch {BatchStart}-{BatchEnd} of {Total}.",
                                batchStart + 1,
                                Math.Min(batchStart + _batchSize, profiles.Count),
                                profiles.Count);
                        }
                    }

                    totalMigrated += profiles.Count;
                }

                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Completed AIProfile migration. Migrated {Count} profiles to individual documents.", totalMigrated);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while migrating AIProfile documents from DictionaryDocument to individual documents.");

                throw;
            }
        });

        return 1;
    }
}
