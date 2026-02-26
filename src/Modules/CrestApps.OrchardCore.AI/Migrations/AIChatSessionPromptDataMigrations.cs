using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Dapper;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// Migrates existing AIChatSession documents by extracting their embedded Prompts
/// into separate AIChatSessionPrompt documents stored via the prompt store.
/// This runs as a deferred task to ensure index tables are created first.
/// </summary>
internal sealed class AIChatSessionPromptDataMigrations : DataMigration
{
    private const int _batchSize = 50;
    private const string _sessionDocumentType = "CrestApps.OrchardCore.AI.Models.AIChatSession, CrestApps.OrchardCore.AI.Abstractions";

#pragma warning disable CA1822 // Mark members as static
    public int Create()
#pragma warning restore CA1822 // Mark members as static
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var store = scope.ServiceProvider.GetRequiredService<IStore>();
            var dbConnectionAccessor = scope.ServiceProvider.GetRequiredService<IDbConnectionAccessor>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AIChatSessionPromptDataMigrations>>();

            var dialect = store.Configuration.SqlDialect;

            var documentTableName = store.Configuration.TableNameConvention.GetDocumentTable(AIConstants.CollectionName);
            var table = $"{store.Configuration.TablePrefix}{documentTableName}";
            var quotedTableName = dialect.QuoteForTableName(table, store.Configuration.Schema);

            var quotedIdColumnName = dialect.QuoteForColumnName(nameof(Document.Id));
            var quotedTypeColumnName = dialect.QuoteForColumnName(nameof(Document.Type));
            var quotedContentColumnName = dialect.QuoteForColumnName(nameof(Document.Content));

            await using var connection = dbConnectionAccessor.CreateConnection();
            await connection.OpenAsync();

            // Query all AIChatSession documents from the AI collection.
            var sqlBuilder = new SqlBuilder(store.Configuration.TablePrefix, store.Configuration.SqlDialect);
            sqlBuilder.AddSelector(quotedIdColumnName);
            sqlBuilder.AddSelector("," + quotedContentColumnName);
            sqlBuilder.From(quotedTableName);
            sqlBuilder.WhereAnd($" {quotedTypeColumnName} = '{_sessionDocumentType}' ");

            try
            {
                var documents = (await connection.QueryAsync<Document>(sqlBuilder.ToSqlString())).ToList();

                if (documents.Count == 0)
                {
                    return;
                }

                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Found {Count} AIChatSession documents to migrate prompts from.", documents.Count);
                }

                // Process in batches to avoid holding too many changes in memory.
                for (var batchStart = 0; batchStart < documents.Count; batchStart += _batchSize)
                {
                    var batch = documents.Skip(batchStart).Take(_batchSize).ToList();

                    using var session = store.CreateSession();

                    foreach (var document in batch)
                    {
                        try
                        {
                            var jsonObject = JsonNode.Parse(document.Content);

                            if (jsonObject is not JsonObject sessionObject)
                            {
                                continue;
                            }

                            if (sessionObject["Prompts"] is not JsonArray promptsArray || promptsArray.Count == 0)
                            {
                                continue;
                            }

                            var sessionId = sessionObject["SessionId"]?.GetValue<string>();

                            if (string.IsNullOrEmpty(sessionId))
                            {
                                continue;
                            }

                            var createdUtc = DateTime.UtcNow;
                            if (sessionObject["CreatedUtc"] is JsonValue createdValue)
                            {
                                try
                                {
                                    createdUtc = createdValue.GetValue<DateTime>();
                                }
                                catch
                                {
                                    // Use default if parsing fails.
                                }
                            }

                            foreach (var promptNode in promptsArray)
                            {
                                if (promptNode is not JsonObject promptObject)
                                {
                                    continue;
                                }

                                var prompt = new AIChatSessionPrompt
                                {
                                    ItemId = promptObject["Id"]?.GetValue<string>()
                                        ?? promptObject["ItemId"]?.GetValue<string>()
                                        ?? IdGenerator.GenerateId(),
                                    SessionId = sessionId,
                                    Role = new ChatRole(promptObject["Role"]?.GetValue<string>() ?? "user"),
                                    Content = promptObject["Content"]?.GetValue<string>(),
                                    IsGeneratedPrompt = promptObject["IsGeneratedPrompt"]?.GetValue<bool>() ?? false,
                                    CreatedUtc = createdUtc,
                                };

                                await session.SaveAsync(prompt, collection: AIConstants.CollectionName);
                            }

                            // Remove the Prompts property from the original document.
                            sessionObject.Remove("Prompts");

                            var updatedContent = store.Configuration.ContentSerializer.Serialize(sessionObject);

                            await connection.ExecuteAsync(
                                $"update {quotedTableName} set {quotedContentColumnName} = @content where {quotedIdColumnName} = @id",
                                new
                                {
                                    content = updatedContent,
                                    id = document.Id,
                                }
                            );
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to migrate prompts for document ID {DocumentId}.", document.Id);
                        }
                    }

                    await session.SaveChangesAsync();

                    if (logger.IsEnabled(LogLevel.Information))
                    {
                        logger.LogInformation("Migrated prompt batch {BatchStart}-{BatchEnd} of {Total} session documents.",
                            batchStart + 1,
                            Math.Min(batchStart + _batchSize, documents.Count),
                            documents.Count);
                    }
                }

                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Completed migrating prompts from {Count} AIChatSession documents.", documents.Count);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while migrating AIChatSession prompts to separate documents.");

                throw;
            }
        });

        return 1;
    }
}
