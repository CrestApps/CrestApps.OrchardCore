using CrestApps.OrchardCore.Learning.Xapi.Core.Indexes;
using CrestApps.OrchardCore.Learning.Xapi.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Xapi.Migrations;

/// <summary>
/// Creates the index tables of statements, documents and activity definitions. Step 2 indexes the account name of each
/// document's agent (review round 9, M2) and maps the existing documents again after the migration.
/// </summary>
internal sealed class XapiMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await XapiIndexSchema.CreateAllAsync(SchemaBuilder);

        return 2;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await XapiDocumentIndexSchema.AddAccountNameAsync(SchemaBuilder);

        // The documents are mapped again once the migration is done, in a scope of their own.
        ShellScope.AddDeferredTask(async scope =>
        {
            var session = scope.ServiceProvider.GetRequiredService<ISession>();

            foreach (var document in await session.Query<XapiDocument>(collection: XapiConstants.CollectionName).ListAsync())
            {
                await session.SaveAsync(document, false, XapiConstants.CollectionName);
            }

            await session.SaveChangesAsync();
        });

        return 2;
    }
}
