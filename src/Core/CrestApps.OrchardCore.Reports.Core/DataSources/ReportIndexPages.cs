using System.Runtime.CompilerServices;
using YesSql;

namespace CrestApps.OrchardCore.Reports.Designer.DataSources;

/// <summary>
/// Reads the records of a YesSql query a page at a time, for the <c>LoadAsync</c> of a data set that reads an index:
/// any number of records can be read, and each page leaves the session once it was read, so a long read doesn't keep
/// every record in memory.
/// </summary>
public static class ReportIndexPages
{
    /// <summary>
    /// The number of records read per page.
    /// </summary>
    public const int PageSize = 500;

    /// <summary>
    /// Reads the records of a query, in its order, a page at a time, until the enumeration stops.
    /// </summary>
    /// <typeparam name="TRecord">The type of the records.</typeparam>
    /// <param name="session">The session the query reads from.</param>
    /// <param name="query">Creates the query, with its conditions and a stable order; it is called once per page.</param>
    /// <param name="collection">The collection the query reads, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The records.</returns>
    public static async IAsyncEnumerable<TRecord> ReadAsync<TRecord>(
        ISession session,
        Func<IQuery<TRecord>> query,
        string collection = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TRecord : class
    {
        await foreach (var page in ReadPagesAsync(session, query, collection, cancellationToken))
        {
            foreach (var record in page)
            {
                yield return record;
            }
        }
    }

    /// <summary>
    /// Reads the records of a query, in its order, a page at a time, until the enumeration stops, such as to look up
    /// what the records of a page refer to at once. A page leaves the session when the next one is read.
    /// </summary>
    /// <typeparam name="TRecord">The type of the records.</typeparam>
    /// <param name="session">The session the query reads from.</param>
    /// <param name="query">Creates the query, with its conditions and a stable order; it is called once per page.</param>
    /// <param name="collection">The collection the query reads, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The pages of records.</returns>
    public static async IAsyncEnumerable<IReadOnlyList<TRecord>> ReadPagesAsync<TRecord>(
        ISession session,
        Func<IQuery<TRecord>> query,
        string collection = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TRecord : class
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(query);

        for (var skip = 0; ; skip += PageSize)
        {
            var page = (await query().Skip(skip).Take(PageSize).ListAsync(cancellationToken)).ToList();

            if (page.Count > 0)
            {
                yield return page;
            }

            session.Detach(page, collection);

            if (page.Count < PageSize)
            {
                yield break;
            }
        }
    }
}
