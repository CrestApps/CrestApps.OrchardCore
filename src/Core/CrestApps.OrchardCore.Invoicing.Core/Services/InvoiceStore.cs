using System.Linq.Expressions;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Stores invoices in the <c>Invoicing</c> collection.
/// </summary>
public interface IInvoiceStore : ICatalog<Invoice>
{
    /// <summary>
    /// Finds the invoice that posted a transaction.
    /// </summary>
    Task<Invoice> FindByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an issued invoice by its number.
    /// </summary>
    Task<Invoice> FindByNumberAsync(string number, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the invoice issued last, or <see langword="null"/> when none was.
    /// </summary>
    Task<Invoice> FindLastIssuedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of invoices, newest first.
    /// </summary>
    Task<PageResult<Invoice>> PageAsync(int page, int pageSize, InvoiceQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// The default <see cref="IInvoiceStore"/>, with concurrency checks, so two commands never overwrite each other.
/// </summary>
public sealed class InvoiceStore : DocumentCatalog<Invoice, InvoiceIndex>, IInvoiceStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvoiceStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="clock">The clock.</param>
    public InvoiceStore(ISession session, IClock clock)
        : base(session, clock)
    {
        CollectionName = InvoicingConstants.CollectionName;
    }

    /// <inheritdoc/>
    protected override bool CheckConcurrency => true;

    /// <inheritdoc/>
    public Task<Invoice> FindByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(transactionId)
            ? Task.FromResult<Invoice>(null)
            : Session.Query<Invoice, InvoiceIndex>(index => index.TransactionId == transactionId, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<Invoice> FindByNumberAsync(string number, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(number)
            ? Task.FromResult<Invoice>(null)
            : Session.Query<Invoice, InvoiceIndex>(index => index.Number == number.Trim(), collection: CollectionName).FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<Invoice> FindLastIssuedAsync(CancellationToken cancellationToken = default)
        => Session.Query<Invoice, InvoiceIndex>(index => index.IssuedUtc != null, collection: CollectionName)
            .OrderByDescending(index => index.IssuedUtc)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<PageResult<Invoice>> PageAsync(int page, int pageSize, InvoiceQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var records = Session.Query<Invoice, InvoiceIndex>(collection: CollectionName);

        if (query.Status.HasValue)
        {
            var status = (int)query.Status.Value;
            records = records.Where(index => index.Status == status);
        }

        if (query.Settlement.HasValue)
        {
            var settlement = (int)query.Settlement.Value;
            records = records.Where(index => index.Settlement == settlement);
        }

        if (query.OverdueOn is DateOnly overdueOn)
        {
            var open = (int)InvoiceStatus.Open;
            var paid = (int)InvoiceSettlement.Paid;
            var dueBefore = overdueOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            records = records.Where(index => index.Status == open && index.Settlement != paid && index.DueDate < dueBefore);
        }

        if (!string.IsNullOrEmpty(query.SourceType))
        {
            records = records.Where(index => index.SourceType == query.SourceType);
        }

        if (!string.IsNullOrEmpty(query.SourceId))
        {
            records = records.Where(index => index.SourceId == query.SourceId);
        }

        var billTo = (query.BillTo ?? []).Where(party => !party.IsEmpty).Distinct().ToArray();

        if (query.OnlyBillTo && billTo.Length == 0)
        {
            return new PageResult<Invoice> { Count = 0, Entries = [] };
        }

        if (query.IssuedOnly)
        {
            records = records.Where(index => index.IssuedUtc != null);
        }

        if (billTo.Length > 0)
        {
            Expression<Func<InvoiceIndex, bool>> predicate = null;

            foreach (var group in billTo.GroupBy(party => party.Kind, StringComparer.Ordinal))
            {
                var kind = group.Key;
                var ids = group.Select(party => party.Id).Distinct(StringComparer.Ordinal).ToArray();
                Expression<Func<InvoiceIndex, bool>> ofKind = index => index.BillToKind == kind && index.BillToId.IsIn(ids);

                predicate = IndexPredicates.OrElse(predicate, ofKind);
            }

            records = records.Where(predicate);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            records = records.Where(index => index.Number.Contains(search) || index.BillToName.Contains(search) || index.Reference.Contains(search));
        }

        var ordered = records
            .OrderByDescending(index => index.CreatedUtc)
            .ThenByDescending(index => index.ItemId);

        var skip = (Math.Max(page, 1) - 1) * pageSize;

        return new PageResult<Invoice>
        {
            Count = await ordered.CountAsync(cancellationToken),
            Entries = (await ordered.Skip(skip).Take(pageSize).ListAsync(cancellationToken)).ToArray(),
        };
    }
}
