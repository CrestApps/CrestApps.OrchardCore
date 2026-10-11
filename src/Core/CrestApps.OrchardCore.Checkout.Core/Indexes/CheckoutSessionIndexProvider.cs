using CrestApps.OrchardCore.Checkout;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Checkout.Core.Indexes;

/// <summary>
/// Maps <see cref="CheckoutSession"/> documents to <see cref="CheckoutSessionIndex"/> rows.
/// </summary>
public sealed class CheckoutSessionIndexProvider : IndexProvider<CheckoutSession>
{
    /// <inheritdoc/>
    public override void Describe(DescribeContext<CheckoutSession> context)
    {
        context.For<CheckoutSessionIndex>()
            .Map(session => new CheckoutSessionIndex
            {
                SessionId = session.SessionId,
                ReferenceType = session.ReferenceType,
                ReferenceId = session.ReferenceId,
                ReferenceVersionId = session.ReferenceVersionId,
                OwnerId = session.OwnerId,
                Status = session.Status,
                CreatedUtc = session.CreatedUtc,
                ModifiedUtc = session.ModifiedUtc,
                CompletedUtc = session.CompletedUtc,
            });
    }
}
