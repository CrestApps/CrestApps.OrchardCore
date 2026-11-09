using CrestApps.OrchardCore.Transactions.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Transactions.Core.Indexes;

/// <summary>
/// Maps a <see cref="PaymentIssue"/> to its <see cref="PaymentIssueIndex"/>.
/// </summary>
public sealed class PaymentIssueIndexProvider : IndexProvider<PaymentIssue>
{
    /// <inheritdoc/>
    public override void Describe(DescribeContext<PaymentIssue> context)
    {
        context.For<PaymentIssueIndex>()
            .Map(issue => new PaymentIssueIndex
            {
                ItemId = issue.ItemId,
                Kind = issue.Kind?.Length > 64 ? issue.Kind[..64] : issue.Kind,
                Status = issue.Status,
                CreatedUtc = issue.CreatedUtc,
            });
    }
}
