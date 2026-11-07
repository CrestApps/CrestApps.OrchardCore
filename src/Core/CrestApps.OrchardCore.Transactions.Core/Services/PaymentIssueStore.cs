using CrestApps.Core.Models;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// The YesSql-backed <see cref="IPaymentIssueStore"/>.
/// </summary>
internal sealed class PaymentIssueStore : DocumentCatalog<PaymentIssue, PaymentIssueIndex>, IPaymentIssueStore
{
    public PaymentIssueStore(ISession session)
        : base(session)
    {
    }

    public async Task<PageResult<PaymentIssue>> PageAsync(int page, int pageSize, PaymentIssueStatus? status, CancellationToken cancellationToken = default)
    {
        var records = Session.Query<PaymentIssue, PaymentIssueIndex>();

        if (status.HasValue)
        {
            records = records.Where(index => index.Status == status.Value);
        }

        var count = await records.CountAsync(cancellationToken);
        var entries = await records
            .OrderByDescending(index => index.CreatedUtc)
            .ThenByDescending(index => index.Id)
            .Skip((Math.Max(page, 1) - 1) * pageSize)
            .Take(pageSize)
            .ListAsync(cancellationToken);

        return new PageResult<PaymentIssue>
        {
            Count = count,
            Entries = [.. entries],
        };
    }

    public Task<int> CountOpenAsync(CancellationToken cancellationToken = default)
        => Session.Query<PaymentIssue, PaymentIssueIndex>(index => index.Status == PaymentIssueStatus.Open).CountAsync(cancellationToken);
}
