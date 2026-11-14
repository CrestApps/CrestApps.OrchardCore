using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Customers.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// Store credit is personal data: its activity reasons carry the titles of the transactions credit was kept or spent for.
/// A person's credit is exported, and erasing them scrubs the reasons and actors while the amounts stay.
/// </summary>
public sealed class StoreCreditPersonalDataContributorTests
{
    private readonly StoreCreditAccount _account = new()
    {
        ItemId = "credit-1",
        CustomerId = "customer-1",
        Currency = "USD",
        Balance = 25m,
        Activity =
        [
            new StoreCreditActivity { CreatedUtc = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc), Amount = 25m, Balance = 25m, Reason = "Paid above what Invoice for Ann Lee owed", Actor = "Ann Lee" },
        ],
    };

    [Fact]
    public async Task Export_IncludesEachBalanceAndItsActivity()
    {
        // Arrange
        var export = new CommercePersonalDataExport();

        // Act
        await CreateContributor(out _).ExportAsync(new CommercePersonalDataExportContext(Subject(), export), TestContext.Current.CancellationToken);

        // Assert
        var record = Assert.Single(export.Records);
        Assert.Equal((StoreCreditPersonalDataContributor.RecordKind, "USD", "25"), (record.Kind, record.Values[nameof(StoreCreditAccount.Currency)], record.Values[nameof(StoreCreditAccount.Balance)]));
        Assert.Contains("Paid above what Invoice for Ann Lee owed", record.Values["Activity1"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Anonymize_ScrubsTheReasonsAndActors_AndKeepsTheAmounts()
    {
        // Arrange
        var contributor = CreateContributor(out var store);
        var result = new CommercePersonalDataAnonymizeResult();

        // Act
        await contributor.AnonymizeAsync(new CommercePersonalDataAnonymizeContext(Subject(), "[removed]", result), TestContext.Current.CancellationToken);

        // Assert
        var entry = Assert.Single(_account.Activity);
        Assert.DoesNotContain("Ann Lee", entry.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("Ann Lee", entry.Actor ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal((25m, 25m), (entry.Amount, _account.Balance));
        store.Verify(candidate => candidate.UpdateAsync(_account, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CommercePersonalDataSubject Subject()
    {
        var subject = new CommercePersonalDataSubject(CustomerOwner.ForUser("user-1"), "ann@example.com");
        subject.AddName("Ann Lee");

        return subject;
    }

    private StoreCreditPersonalDataContributor CreateContributor(out Mock<IStoreCreditStore> store)
    {
        var customers = new Mock<ICustomerStore>();
        customers.Setup(candidate => candidate.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>())).ReturnsAsync(new Customer { ItemId = "customer-1", UserId = "user-1" });

        store = new Mock<IStoreCreditStore>();
        store.Setup(candidate => candidate.ListByCustomerAsync("customer-1", It.IsAny<CancellationToken>())).ReturnsAsync([_account]);

        var archive = new Mock<CrestApps.OrchardCore.Checkout.Core.Services.IBalanceLedgerArchive>();
        archive.Setup(candidate => candidate.ListAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        return new StoreCreditPersonalDataContributor(customers.Object, store.Object, archive.Object);
    }
}
