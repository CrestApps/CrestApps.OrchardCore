using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using Dapper;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// Handles direct upgrades from v1 chat-session documents without changing the
/// preview-era prompt extraction migration path.
/// </summary>
internal sealed class AIChatSessionV1PromptDataMigrations : DataMigration
{
    private const int _batchSize = 50;
    private const string _legacySessionDocumentTypePrefix = "CrestApps.OrchardCore.AI.Models.AIChatSession,";
    private const string _currentSessionDocumentTypePrefix = "CrestApps.Core.AI.Models.AIChatSession,";

    /// <summary>
    /// Creates a new .
    /// </summary>
    public static int Create()
    {
        ShellScope.AddDeferredTask(scope => MigrateV1SessionsAsync(scope.ServiceProvider));

        return 1;
    }

    private static async Task MigrateV1SessionsAsync(IServiceProvider serviceProvider)
    {
        var store = serviceProvider.GetRequiredService<IStore>();
        var dbConnectionAccessor = serviceProvider.GetRequiredService<IDbConnectionAccessor>();
        var logger = serviceProvider.GetRequiredService<ILogger<AIChatSessionV1PromptDataMigrations>>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var yesSqlStoreOptions = serviceProvider.GetRequiredService<IOptions<YesSqlStoreOptions>>().Value;

        var dialect = store.Configuration.SqlDialect;
        var documentTableName = store.Configuration.TableNameConvention.GetDocumentTable(yesSqlStoreOptions.AICollectionName);
        var table = $"{store.Configuration.TablePrefix}{documentTableName}";
        var quotedTableName = dialect.QuoteForTableName(table, store.Configuration.Schema);
        var quotedIdColumnName = dialect.QuoteForColumnName(nameof(Document.Id));
        var quotedTypeColumnName = dialect.QuoteForColumnName(nameof(Document.Type));
        var quotedContentColumnName = dialect.QuoteForColumnName(nameof(Document.Content));

        await using var connection = dbConnectionAccessor.CreateConnection();
        await connection.OpenAsync();

        var sqlBuilder = new SqlBuilder(store.Configuration.TablePrefix, store.Configuration.SqlDialect);
        sqlBuilder.AddSelector(quotedIdColumnName);
        sqlBuilder.AddSelector("," + quotedContentColumnName);
        sqlBuilder.From(quotedTableName);
        sqlBuilder.WhereAnd(
            $" ({quotedTypeColumnName} LIKE '{_legacySessionDocumentTypePrefix}%' OR {quotedTypeColumnName} LIKE '{_currentSessionDocumentTypePrefix}%') ");

        var documents = (await connection.QueryAsync<Document>(sqlBuilder.ToSqlString())).ToList();

        if (documents.Count == 0)
        {
            return;
        }

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

                    var documentUpdated = NormalizePersistedSessionDocument(sessionObject);
                    var promptsArray = sessionObject["Prompts"] as JsonArray;
                    var sessionId = sessionObject[nameof(AIChatSession.SessionId)]?.GetValue<string>();

                    if (string.IsNullOrEmpty(sessionId) || promptsArray is null || promptsArray.Count == 0)
                    {
                        if (!documentUpdated)
                        {
                            continue;
                        }
                    }

                    var createdUtc = clock.UtcNow;

                    if (sessionObject[nameof(AIChatSession.CreatedUtc)] is JsonValue createdValue)
                    {
                        try
                        {
                            createdUtc = createdValue.GetValue<DateTime>();
                        }
                        catch
                        {
                            createdUtc = clock.UtcNow;
                        }
                    }

                    if (!string.IsNullOrEmpty(sessionId) && promptsArray is not null)
                    {
                        foreach (var promptNode in promptsArray)
                        {
                            if (promptNode is not JsonObject promptObject)
                            {
                                continue;
                            }

                            var promptCreatedUtc = createdUtc;

                            if (promptObject[nameof(AIChatSessionPrompt.CreatedUtc)] is JsonValue promptCreatedValue)
                            {
                                try
                                {
                                    promptCreatedUtc = promptCreatedValue.GetValue<DateTime>();
                                }
                                catch
                                {
                                    promptCreatedUtc = createdUtc;
                                }
                            }

                            var prompt = new AIChatSessionPrompt
                            {
                                ItemId = promptObject["Id"]?.GetValue<string>()
                                    ?? promptObject["ItemId"]?.GetValue<string>()
                                    ?? UniqueId.GenerateId(),
                                SessionId = sessionId,
                                Role = new ChatRole(promptObject["Role"]?.GetValue<string>() ?? "user"),
                                Title = promptObject[nameof(AIChatSessionPrompt.Title)]?.GetValue<string>(),
                                Content = promptObject[nameof(AIChatSessionPrompt.Content)]?.GetValue<string>(),
                                IsGeneratedPrompt = promptObject[nameof(AIChatSessionPrompt.IsGeneratedPrompt)]?.GetValue<bool>() ?? false,
                                CreatedUtc = promptCreatedUtc,
                                ContentItemIds = DeserializeOrDefault<List<string>>(promptObject[nameof(AIChatSessionPrompt.ContentItemIds)]),
                                References = DeserializeOrDefault<Dictionary<string, AICompletionReference>>(promptObject[nameof(AIChatSessionPrompt.References)]),
                            };

                            await session.SaveAsync(prompt, collection: yesSqlStoreOptions.AICollectionName);
                        }

                        sessionObject.Remove("Prompts");
                        documentUpdated = true;
                    }

                    if (!documentUpdated)
                    {
                        continue;
                    }

                    var updatedContent = store.Configuration.ContentSerializer.Serialize(sessionObject);

                    await connection.ExecuteAsync(
                        $"UPDATE {quotedTableName} SET {quotedContentColumnName} = @Content WHERE {quotedIdColumnName} = @Id",
                        new
                        {
                            document.Id,
                            Content = updatedContent,
                        });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to migrate v1 chat-session prompts for document ID {DocumentId}.", document.Id);
                }
            }

            await session.SaveChangesAsync();
        }
    }

    private static bool NormalizePersistedSessionDocument(JsonObject sessionObject)
    {
        var updated = false;

        if (sessionObject[nameof(AIChatSession.LastActivityUtc)] is null &&
            sessionObject[nameof(AIChatSession.CreatedUtc)] is JsonNode createdUtc)
        {
            sessionObject[nameof(AIChatSession.LastActivityUtc)] = createdUtc.DeepClone();
            updated = true;
        }

        return updated;
    }

    private static T DeserializeOrDefault<T>(JsonNode node)
        where T : new()
    {
        if (node is null)
        {
            return new T();
        }

        return JsonSerializer.Deserialize<T>(node.ToJsonString()) ?? new T();
    }
}
