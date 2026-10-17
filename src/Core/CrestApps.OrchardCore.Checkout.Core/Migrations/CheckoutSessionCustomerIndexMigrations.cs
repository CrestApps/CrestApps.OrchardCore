using CrestApps.OrchardCore.Checkout.Core.Indexes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Checkout.Core.Migrations;

/// <summary>
/// Creates the <see cref="CheckoutSessionCustomerIndex"/> table that finds a person's checkout sessions by owner and by
/// email, and indexes the sessions that were saved before it existed.
/// </summary>
public sealed class CheckoutSessionCustomerIndexMigrations : DataMigration
{
    /// <summary>
    /// The number of sessions indexed per unit of work when existing sessions are indexed.
    /// </summary>
    public const int BackfillBatchSize = 100;

    /// <summary>
    /// Creates the index table, then indexes the existing sessions once the migration has committed.
    /// </summary>
    /// <returns>The schema version this migration creates.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CheckoutSessionCustomerIndex>(table => table
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("OwnerId")
            .Column<string>("NormalizedEmail", column => column.WithLength(CheckoutSessionCustomerIndexProvider.EmailLength))
            .Column<DateTime>("CreatedUtc")
        );

        await SchemaBuilder.AlterIndexTableAsync<CheckoutSessionCustomerIndex>(table => table
            .CreateIndex("IDX_CheckoutSessionCustomerIndex_Owner", "OwnerId", "CreatedUtc")
        );

        await SchemaBuilder.AlterIndexTableAsync<CheckoutSessionCustomerIndex>(table => table
            .CreateIndex("IDX_CheckoutSessionCustomerIndex_Email", "NormalizedEmail", "CreatedUtc")
        );

        // A session is indexed when it is saved, so the sessions saved before this table existed are saved once more.
        // That runs after the migration commits, when the table can be written to. Outside a shell, as in a schema
        // test, there is nothing to defer to and the caller indexes the sessions itself.
        if (ShellScope.Current is not null)
        {
            ShellScope.AddDeferredTask(scope => BackfillAsync(scope.ServiceProvider.GetRequiredService<IStore>(), CancellationToken.None));
        }

        return 1;
    }

    /// <summary>
    /// Saves every existing checkout session again, a batch per unit of work, so the index providers registered since
    /// it was last saved index it.
    /// </summary>
    /// <param name="store">The tenant's YesSql store.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of sessions saved.</returns>
    public static async Task<int> BackfillAsync(IStore store, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);

        var count = 0;

        for (var skip = 0; ; skip += BackfillBatchSize)
        {
            await using var session = store.CreateSession();

            var sessions = await session.Query<CheckoutSession, CheckoutSessionIndex>()
                .OrderBy(x => x.CreatedUtc)
                .ThenBy(x => x.SessionId)
                .Skip(skip)
                .Take(BackfillBatchSize)
                .ListAsync(cancellationToken);

            var batch = 0;

            foreach (var checkoutSession in sessions)
            {
                await session.SaveAsync(checkoutSession, cancellationToken: cancellationToken);
                batch++;
            }

            await session.SaveChangesAsync(cancellationToken);

            count += batch;

            if (batch < BackfillBatchSize)
            {
                break;
            }
        }

        return count;
    }
}
