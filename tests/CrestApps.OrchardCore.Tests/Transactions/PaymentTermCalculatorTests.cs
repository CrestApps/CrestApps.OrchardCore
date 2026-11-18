using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// A payment term turns the day a document is issued into the day it is due, and may offer a discount for paying
/// early. Every date is a local date, so a term never moves a day because of a time zone.
/// </summary>
public sealed class PaymentTermCalculatorTests
{
    private readonly PaymentTermCalculator _calculator = new();

    [Theory]
    [InlineData(PaymentTermKind.DueOnReceipt, 0, 0, "2026-03-14", "2026-03-14")]
    [InlineData(PaymentTermKind.NetDays, 30, 0, "2026-03-14", "2026-04-13")]
    [InlineData(PaymentTermKind.NetDays, 15, 0, "2026-12-20", "2027-01-04")]
    [InlineData(PaymentTermKind.EndOfMonth, 0, 0, "2026-02-03", "2026-02-28")]
    [InlineData(PaymentTermKind.EndOfMonth, 15, 0, "2026-02-03", "2026-03-15")]
    [InlineData(PaymentTermKind.EndOfMonth, 0, 0, "2028-02-29", "2028-02-29")]
    [InlineData(PaymentTermKind.DayOfNextMonth, 0, 10, "2026-01-31", "2026-02-10")]
    [InlineData(PaymentTermKind.DayOfNextMonth, 0, 31, "2026-01-15", "2026-02-28")]
    [InlineData(PaymentTermKind.DayOfNextMonth, 0, 5, "2026-12-09", "2027-01-05")]
    public void GetDueDate_FollowsTheKindOfTerm(PaymentTermKind kind, int days, int dayOfMonth, string issued, string expected)
    {
        // Arrange
        var term = new PaymentTerm { Kind = kind, Days = days, DayOfMonth = dayOfMonth };

        // Act
        var due = _calculator.GetDueDate(term, DateOnly.Parse(issued));

        // Assert
        Assert.Equal(DateOnly.Parse(expected), due);
    }

    [Fact]
    public void GetDueDate_WithoutATerm_IsTheIssueDate()
    {
        // Act
        var due = _calculator.GetDueDate(null, new DateOnly(2026, 5, 1));

        // Assert
        Assert.Equal(new DateOnly(2026, 5, 1), due);
    }

    [Fact]
    public void GetDueDate_NeverFallsBeforeTheIssueDate()
    {
        // Arrange
        var term = new PaymentTerm { Kind = PaymentTermKind.NetDays, Days = -5 };

        // Act
        var due = _calculator.GetDueDate(term, new DateOnly(2026, 5, 1));

        // Assert
        Assert.Equal(new DateOnly(2026, 5, 1), due);
    }

    [Fact]
    public void GetEarlyPaymentDiscount_InsideTheWindow_TakesThePercentRoundedToTheCurrency()
    {
        // Arrange: 2% if paid within 10 days.
        var term = new PaymentTerm { Kind = PaymentTermKind.NetDays, Days = 30, DiscountPercent = 2m, DiscountDays = 10 };

        // Act
        var discount = _calculator.GetEarlyPaymentDiscount(term, new DateOnly(2026, 3, 1), 1234.56m, "USD");

        // Assert
        Assert.NotNull(discount);
        Assert.Equal(new DateOnly(2026, 3, 11), discount.Value.Deadline);
        Assert.Equal(24.69m, discount.Value.Amount);
        Assert.True(discount.Value.AppliesOn(new DateOnly(2026, 3, 11)));
        Assert.False(discount.Value.AppliesOn(new DateOnly(2026, 3, 12)));
    }

    [Fact]
    public void GetEarlyPaymentDiscount_ForAZeroDecimalCurrency_RoundsToWholeUnits()
    {
        // Arrange
        var term = new PaymentTerm { Kind = PaymentTermKind.NetDays, Days = 30, DiscountPercent = 1.5m, DiscountDays = 7 };

        // Act
        var discount = _calculator.GetEarlyPaymentDiscount(term, new DateOnly(2026, 3, 1), 10_001m, "JPY");

        // Assert
        Assert.Equal(150m, discount.Value.Amount);
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData(0, 10)]
    [InlineData(2, null)]
    [InlineData(2, 0)]
    public void GetEarlyPaymentDiscount_WithoutAPercentOrAWindow_OffersNone(int? percent, int? days)
    {
        // Arrange
        var term = new PaymentTerm { Kind = PaymentTermKind.NetDays, Days = 30, DiscountPercent = percent, DiscountDays = days };

        // Act
        var discount = _calculator.GetEarlyPaymentDiscount(term, new DateOnly(2026, 3, 1), 100m, "USD");

        // Assert
        Assert.Null(discount);
    }
}
