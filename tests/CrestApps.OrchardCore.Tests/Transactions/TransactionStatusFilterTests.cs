using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.ViewModels;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// The status filter offered the Abandoned status, which no code ever sets, so picking it always showed an empty list.
/// </summary>
public sealed class TransactionStatusFilterTests
{
    [Fact]
    public void BuildFilterItems_DoesNotOfferTheAbandonedStatus()
    {
        // Act
        var items = TransactionStatusFilterExtensions.BuildFilterItems(TransactionStatusFilter.All, new PassThroughStringLocalizer<TransactionStatusFilterTests>());

        // Assert
        Assert.DoesNotContain(items, item => item.Value == nameof(TransactionStatusFilter.Abandoned));
        Assert.Contains(items, item => item.Value == nameof(TransactionStatusFilter.Refunded));
    }
}
