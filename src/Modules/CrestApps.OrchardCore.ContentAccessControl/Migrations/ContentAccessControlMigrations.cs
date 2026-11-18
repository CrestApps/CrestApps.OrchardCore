using CrestApps.OrchardCore.ContentAccessControl.Services;
using CrestApps.OrchardCore.Roles.Core.Indexes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;
using YesSql.Services;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContentAccessControl.Migrations;

/// <summary>
/// Creates <see cref="ContentAccessRoleIndex"/> and fills it with the items that were restricted before it existed.
/// </summary>
internal sealed class ContentAccessControlMigrations : DataMigration
{
    private const int BatchSize = 100;

    public async Task<int> CreateAsync()
    {
        await CreateRoleIndexTableAsync(SchemaBuilder);

        // Items restricted before the index existed are indexed again once the feature is running, a batch at a time.
        ShellScope.AddDeferredTask(IndexRestrictedItemsAsync);

        return 1;
    }

    /// <summary>
    /// Creates the table of <see cref="ContentAccessRoleIndex"/>.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task that completes when the table exists.</returns>
    internal static async Task CreateRoleIndexTableAsync(ISchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<ContentAccessRoleIndex>(table => table
            .Column<string>(nameof(ContentAccessRoleIndex.ContentItemId), column => column.WithLength(26))
            .Column<string>(nameof(ContentAccessRoleIndex.ContentType), column => column.WithLength(ContentItemIndex.MaxContentTypeSize))
            .Column<string>(nameof(ContentAccessRoleIndex.RoleName), column => column.WithLength(ContentAccessRoleIndex.MaxRoleNameLength)));

        await schemaBuilder.AlterIndexTableAsync<ContentAccessRoleIndex>(table =>
        {
            table.CreateIndex("IDX_ContentAccessRoleIndex_ContentItemId", nameof(ContentAccessRoleIndex.ContentItemId), nameof(ContentAccessRoleIndex.RoleName));
            table.CreateIndex("IDX_ContentAccessRoleIndex_RoleName", nameof(ContentAccessRoleIndex.RoleName), nameof(ContentAccessRoleIndex.ContentType), "DocumentId");
        });
    }

    private static async Task IndexRestrictedItemsAsync(ShellScope scope)
    {
        var services = scope.ServiceProvider;
        var definitions = services.GetRequiredService<IContentDefinitionManager>();
        var store = services.GetRequiredService<IStore>();
        var logger = services.GetRequiredService<ILogger<ContentAccessControlMigrations>>();
        var types = (await definitions.ListTypeDefinitionsAsync())
            .Where(ContentAccessRestriction.IsRestricted)
            .Select(type => type.Name)
            .ToArray();

        if (types.Length == 0)
        {
            return;
        }

        var lastId = 0L;
        var count = 0;

        while (true)
        {
            // Each batch in its own session, so a large site never holds every item at once. The session gets the
            // scoped index providers as Orchard's own does, this feature's included.
            await using var session = store.CreateSession();
            session.RegisterIndexes([.. services.GetServices<IScopedIndexProvider>()]);
            var items = (await session.Query<ContentItem, ContentItemIndex>(index => index.Published && index.ContentType.IsIn(types) && index.DocumentId > lastId)
                .OrderBy(index => index.DocumentId)
                .Take(BatchSize)
                .ListAsync()).ToList();

            if (items.Count == 0)
            {
                break;
            }

            foreach (var item in items)
            {
                await session.SaveAsync(item, checkConcurrency: false);
            }

            await session.SaveChangesAsync();
            lastId = items[^1].Id;
            count += items.Count;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Content Access Control indexed the roles of {Count} restricted content items.", count);
        }
    }
}
