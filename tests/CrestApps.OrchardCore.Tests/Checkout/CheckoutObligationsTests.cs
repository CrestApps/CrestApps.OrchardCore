using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

public sealed class CheckoutObligationsTests
{
    [Fact]
    public void GetExpectedObligationIds_WhenOnlyOneTimeAmount_ReturnsOneTimeObligation()
    {
        // Arrange
        var invoice = new CheckoutInvoice
        {
            Currency = "USD",
            InitialPaymentAmount = 25d,
            LineItems = [],
        };

        // Act
        var obligations = CheckoutObligations.GetExpectedObligationIds(invoice);

        // Assert
        Assert.Equal([CheckoutObligations.OneTime], obligations);
    }

    [Fact]
    public void GetExpectedObligationIds_WhenOneTimeAmountIsZero_ReturnsNoOneTimeObligation()
    {
        // Arrange
        var invoice = new CheckoutInvoice
        {
            Currency = "USD",
            InitialPaymentAmount = 0d,
            LineItems = [],
        };

        // Act
        var obligations = CheckoutObligations.GetExpectedObligationIds(invoice);

        // Assert
        Assert.Empty(obligations);
    }

    [Fact]
    public void GetExpectedObligationIds_GroupsRecurringLinesByIntervalIntoDistinctObligations()
    {
        // Arrange: two lines share the monthly interval and one is yearly, so there are two recurring
        // obligations, not three.
        var monthly = new BillingDurationKey(DurationType.Month, 1);
        var yearly = new BillingDurationKey(DurationType.Year, 1);

        var invoice = new CheckoutInvoice
        {
            Currency = "USD",
            LineItems =
            [
                new CheckoutLineItem { Id = "a", Quantity = 1, UnitPrice = 10d, Plan = new RecurringPlan { DurationType = DurationType.Month, BillingDuration = 1 } },
                new CheckoutLineItem { Id = "b", Quantity = 1, UnitPrice = 20d, Plan = new RecurringPlan { DurationType = DurationType.Month, BillingDuration = 1 } },
                new CheckoutLineItem { Id = "c", Quantity = 1, UnitPrice = 30d, Plan = new RecurringPlan { DurationType = DurationType.Year, BillingDuration = 1 } },
            ],
        };

        // Act
        var obligations = CheckoutObligations.GetExpectedObligationIds(invoice);

        // Assert
        Assert.Equal(2, obligations.Count);
        Assert.Contains(CheckoutObligations.Recurring(monthly), obligations);
        Assert.Contains(CheckoutObligations.Recurring(yearly), obligations);
    }

    [Fact]
    public void GetExpectedObligationIds_CombinesOneTimeAndRecurringObligations()
    {
        // Arrange
        var invoice = new CheckoutInvoice
        {
            Currency = "USD",
            InitialPaymentAmount = 5d,
            LineItems =
            [
                new CheckoutLineItem { Id = "a", Quantity = 1, UnitPrice = 10d, Plan = new RecurringPlan { DurationType = DurationType.Month, BillingDuration = 1 } },
            ],
        };

        // Act
        var obligations = CheckoutObligations.GetExpectedObligationIds(invoice);

        // Assert
        Assert.Contains(CheckoutObligations.OneTime, obligations);
        Assert.Contains(CheckoutObligations.Recurring(new BillingDurationKey(DurationType.Month, 1)), obligations);
        Assert.Equal(2, obligations.Count);
    }

    [Fact]
    public void GetExpectedObligationIds_SkipsZeroValueRecurringIntervals()
    {
        // Arrange: a free recurring interval collects no money, so it must not become a payment obligation.
        var invoice = new CheckoutInvoice
        {
            Currency = "USD",
            LineItems =
            [
                new CheckoutLineItem { Id = "free", Quantity = 1, UnitPrice = 0d, Plan = new RecurringPlan { DurationType = DurationType.Month, BillingDuration = 1 } },
                new CheckoutLineItem { Id = "paid", Quantity = 1, UnitPrice = 30d, Plan = new RecurringPlan { DurationType = DurationType.Year, BillingDuration = 1 } },
            ],
        };

        // Act
        var obligations = CheckoutObligations.GetExpectedObligationIds(invoice);

        // Assert
        Assert.Equal([CheckoutObligations.Recurring(new BillingDurationKey(DurationType.Year, 1))], obligations);
    }
}
