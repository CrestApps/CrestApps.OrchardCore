using CrestApps.OrchardCore.Tickets.Core.Indexes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using YesSql;

namespace CrestApps.OrchardCore.Tickets.Migrations;

/// <summary>
/// Fills index columns added after tickets were indexed, straight in the index table, so a site with many tickets
/// needn't save each one again.
/// </summary>
internal static class TicketIndexBackfill
{
    /// <summary>
    /// Sets <see cref="TicketIndex.DueSortUtc"/> from <see cref="TicketIndex.NextDueUtc"/>, or to
    /// <see cref="TicketIndexProvider.NoDueUtc"/> when nothing is due, on the rows that have none yet.
    /// </summary>
    /// <param name="services">The services of the scope the work runs in.</param>
    /// <returns>A task.</returns>
    public static async Task FillDueSortAsync(IServiceProvider services)
    {
        var store = services.GetRequiredService<IStore>();
        var configuration = store.Configuration;
        var dialect = configuration.SqlDialect;
        var table = dialect.QuoteForTableName(
            $"{configuration.TablePrefix}{configuration.TableNameConvention.GetIndexTable(typeof(TicketIndex), null)}",
            configuration.Schema);
        var dueSort = dialect.QuoteForColumnName(nameof(TicketIndex.DueSortUtc));
        var nextDue = dialect.QuoteForColumnName(nameof(TicketIndex.NextDueUtc));

        await using var connection = services.GetRequiredService<IDbConnectionAccessor>().CreateConnection();
        await connection.OpenAsync();

        await connection.ExecuteAsync($"UPDATE {table} SET {dueSort} = {nextDue} WHERE {dueSort} IS NULL AND {nextDue} IS NOT NULL");
        await connection.ExecuteAsync($"UPDATE {table} SET {dueSort} = @none WHERE {dueSort} IS NULL", new { none = TicketIndexProvider.NoDueUtc });
    }
}
