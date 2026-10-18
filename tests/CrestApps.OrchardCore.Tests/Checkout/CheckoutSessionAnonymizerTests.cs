using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Erasing a buyer from the checkout sessions behind what they paid for: who they were goes from every attempt, the
/// amounts and dates stay for the store's accounts.
/// </summary>
public sealed class CheckoutSessionAnonymizerTests
{
    private static readonly DateTime _now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryCheckoutSessionStore _store = new();

    [Fact]
    public void Anonymize_RemovesTheBuyer_AndKeepsTheAmountsDatesAndTaxLocation()
    {
        // Arrange
        var session = Session("s1", "o1", minutes: 1);
        var modified = session.ModifiedUtc;

        // Act
        session.Anonymize(_now);

        // Assert
        Assert.Null(session.OwnerId);
        Assert.Null(session.IPAddress);
        Assert.Null(session.AgentInfo);
        Assert.Empty(session.SavedSteps);
        Assert.True(session.TryGet<CheckoutContactInfo>(out var contact));
        Assert.Null(contact.DisplayName);
        Assert.Null(contact.Email);
        Assert.Null(contact.Phone);
        Assert.Equal("US", contact.BillingAddress.Country);
        Assert.Equal("IL", contact.BillingAddress.Region);
        Assert.Null(contact.BillingAddress.AddressLine1);
        Assert.Equal("USD", session.Currency);
        Assert.Equal(CheckoutSessionStatus.Completed, session.Status);
        Assert.Equal(_now, session.AnonymizedUtc);

        // The abandoned-checkout sweep keys off the modification time, so anonymizing must not postpone it.
        Assert.Equal(modified, session.ModifiedUtc);
    }

    [Fact]
    public void Anonymize_ReplacesTheNameWhenAskedTo_AndKeepsTheFirstAnonymizationTime()
    {
        // Arrange
        var session = Session("s1", "o1", minutes: 1);
        session.Anonymize(_now);
        session.Put(new CheckoutContactInfo { DisplayName = "Pat Lee" });

        // Act
        session.Anonymize(_now.AddDays(1), "Deleted customer");

        // Assert
        Assert.True(session.TryGet<CheckoutContactInfo>(out var contact));
        Assert.Equal("Deleted customer", contact.DisplayName);
        Assert.Equal(_now, session.AnonymizedUtc);
    }

    [Fact]
    public async Task AnonymizeByReferenceAsync_ReachesEveryAttempt_AndLeavesOtherReferencesAlone()
    {
        // Arrange
        _store.Seed(Session("s1", "o1", minutes: 1));
        _store.Seed(Session("s2", "o1", minutes: 2));
        _store.Seed(Session("s3", "o2", minutes: 3));

        var anonymizer = CreateAnonymizer();

        // Act
        var count = await anonymizer.AnonymizeByReferenceAsync(CheckoutReferenceTypes.Order, "o1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, count);
        Assert.NotNull((await _store.GetAsync("s1", TestContext.Current.CancellationToken)).AnonymizedUtc);
        Assert.NotNull((await _store.GetAsync("s2", TestContext.Current.CancellationToken)).AnonymizedUtc);

        var other = await _store.GetAsync("s3", TestContext.Current.CancellationToken);
        Assert.Null(other.AnonymizedUtc);
        Assert.Equal("user-1", other.OwnerId);
        Assert.Equal("203.0.113.7", other.IPAddress);
    }

    [Fact]
    public async Task AnonymizeByReferenceAsync_SkipsSessionsAlreadyAnonymized()
    {
        // Arrange
        _store.Seed(Session("s1", "o1", minutes: 1));
        var anonymizer = CreateAnonymizer();
        await anonymizer.AnonymizeByReferenceAsync(CheckoutReferenceTypes.Order, "o1", TestContext.Current.CancellationToken);
        var saves = _store.SaveCount;

        // Act
        var count = await anonymizer.AnonymizeByReferenceAsync(CheckoutReferenceTypes.Order, "o1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, count);
        Assert.Equal(saves, _store.SaveCount);
    }

    [Fact]
    public async Task AnonymizeByReferenceAsync_WorksThroughMoreThanOnePage()
    {
        // Arrange
        // More than two pages of 50.
        const int total = 123;

        for (var i = 0; i < total; i++)
        {
            _store.Seed(Session($"s{i:D3}", "o1", minutes: i));
        }

        // Act
        var count = await CreateAnonymizer().AnonymizeByReferenceAsync(CheckoutReferenceTypes.Order, "o1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(total, count);
    }

    [Fact]
    public async Task AnonymizeAsync_BySessionId_ReturnsWhetherItAnonymizedAnything()
    {
        // Arrange
        _store.Seed(Session("s1", "o1", minutes: 1));
        var anonymizer = CreateAnonymizer();

        // Act
        var first = await anonymizer.AnonymizeAsync("s1", TestContext.Current.CancellationToken);
        var again = await anonymizer.AnonymizeAsync("s1", TestContext.Current.CancellationToken);
        var missing = await anonymizer.AnonymizeAsync("missing", TestContext.Current.CancellationToken);
        var none = await anonymizer.AnonymizeAsync(null, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first);
        Assert.False(again);
        Assert.False(missing);
        Assert.False(none);
    }

    private CheckoutSessionAnonymizer CreateAnonymizer()
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        return new CheckoutSessionAnonymizer(_store, clock.Object);
    }

    private static CheckoutSession Session(string sessionId, string orderId, int minutes)
    {
        var session = new CheckoutSession
        {
            SessionId = sessionId,
            ReferenceType = CheckoutReferenceTypes.Order,
            ReferenceId = orderId,
            Currency = "USD",
            Status = CheckoutSessionStatus.Completed,
            OwnerId = "user-1",
            IPAddress = "203.0.113.7",
            AgentInfo = "Mozilla/5.0",
            CreatedUtc = _now.AddDays(-30).AddMinutes(minutes),
            ModifiedUtc = _now.AddDays(-30).AddMinutes(minutes),
            SavedSteps = new JsonObject
            {
                ["contact"] = new JsonObject { ["email"] = "pat@example.test" },
            },
        };

        session.Put(new CheckoutContactInfo
        {
            DisplayName = "Pat Lee",
            Email = "pat@example.test",
            Phone = "+1 555 0100",
            BillingAddress = new Address { Name = "Pat Lee", AddressLine1 = "1 Main St", City = "Springfield", Region = "IL", PostalCode = "62701", Country = "US" },
        });

        return session;
    }
}
