using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Migrations;

[Obsolete("This class will be removed before the v1 is released.")]
internal sealed class CatalogItemMigrations : DataMigration
{
#pragma warning disable CA1822 // Mark members as static
    public int Create()
#pragma warning restore CA1822 // Mark members as static
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var store = scope.ServiceProvider.GetRequiredService<IStore>();
            var storeCollectionOptions = scope.ServiceProvider.GetRequiredService<IOptions<StoreCollectionOptions>>();
            var dbConnectionAccessor = scope.ServiceProvider.GetRequiredService<IDbConnectionAccessor>();

            var logger = scope.ServiceProvider.GetRequiredService<ILogger<CatalogItemMigrations>>();
            var dialect = store.Configuration.SqlDialect;

            var collections = storeCollectionOptions.Value.Collections.ToHashSet();

            collections.Add("");

            foreach (var collection in collections)
            {
                var documentTableName = store.Configuration.TableNameConvention.GetDocumentTable(collection);
                var table = $"{store.Configuration.TablePrefix}{documentTableName}";
                var quotedTableName = dialect.QuoteForTableName(table, store.Configuration.Schema);

                var quotedIdColumnName = dialect.QuoteForColumnName(nameof(Document.Id));
                var quotedTypeColumnName = dialect.QuoteForColumnName(nameof(Document.Type));
                var quotedContentColumnName = dialect.QuoteForColumnName(nameof(Document.Content));

                var sqlBuilder = new SqlBuilder(store.Configuration.TablePrefix, store.Configuration.SqlDialect);
                sqlBuilder.AddSelector(quotedIdColumnName);
                sqlBuilder.AddSelector("," + quotedTypeColumnName);
                sqlBuilder.AddSelector("," + quotedContentColumnName);
                sqlBuilder.From(quotedTableName);
                sqlBuilder.WhereAnd($" {quotedTypeColumnName} LIKE 'CrestApps.OrchardCore.Models.DictionaryDocument`1[[%' ");

                await using var connection = dbConnectionAccessor.CreateConnection();

                await connection.OpenAsync();
                var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);

                try
                {
                    var documents = await connection.QueryAsync<Document>(sqlBuilder.ToSqlString());

                    foreach (var document in documents)
                    {
                        var jsonObject = JsonNode.Parse(document.Content);

                        if (jsonObject?["Records"] is not JsonObject recordsObject)
                        {
                            continue;
                        }

                        var modified = false;

                        foreach (var record in recordsObject)
                        {
                            if (record.Value is JsonObject recordObject)
                            {
                                if (recordObject.TryGetPropertyValue("Id", out var idNode) && !recordObject.ContainsKey("ItemId"))
                                {
                                    if (idNode?.GetValue<string>() != record.Key)
                                    {
                                        continue;
                                    }

                                    recordObject["ItemId"] = record.Key;
                                    recordObject.Remove("Id");
                                    modified = true;
                                }
                            }
                        }

                        if (modified)
                        {
                            var content = store.Configuration.ContentSerializer.Serialize(jsonObject);

                            await connection.ExecuteAsync(
                                $"update {quotedTableName} set Content = @content where {quotedIdColumnName} = @id",
                                new
                                {
                                    content,
                                    id = document.Id,
                                }
                            );
                        }
                    }

                    await transaction.CommitAsync();
                }
                catch (Exception e)
                {
                    logger.LogError(e, "An error occurred while updating indexing tasks Category to Content.");

                    await transaction.RollbackAsync();
                    throw;
                }
                finally
                {
                    await connection.CloseAsync();
                }
            }
        });

        return 1;
    }
}
