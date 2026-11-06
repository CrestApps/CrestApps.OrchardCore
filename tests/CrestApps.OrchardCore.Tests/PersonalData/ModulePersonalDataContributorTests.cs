using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Marketing.Services;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Tests.Commerce.B2B;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.PersonalData;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;

namespace CrestApps.OrchardCore.Tests.PersonalData;

/// <summary>
/// The personal-data contributors of Workforce, Marketing and business buying export what their module keeps about a
/// person and anonymize it, keeping what the business must keep.
/// </summary>
public sealed class ModulePersonalDataContributorTests
{
    private const string UserId = "user-1";

    [Fact]
    public async Task AWorkersProfile_IsExported_AndAnonymizedKeepingTheEmployeeNumber()
    {
        // Arrange
        var worker = new WorkerProfile
        {
            ItemId = "w1",
            UserId = UserId,
            DisplayName = "Ann Lee",
            Email = "ann@example.com",
            Phone = "+17025550100",
            EmployeeNumber = "E-7",
            DateOfBirth = new DateOnly(1990, 1, 2),
        };

        var workers = new Mock<IWorkerManager>();
        workers.Setup(manager => manager.FindByUserIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(worker);

        var contributor = new WorkerProfilePersonalDataContributor(workers.Object, new PassThroughStringLocalizer<WorkerProfilePersonalDataContributor>());
        var subject = new PersonalDataSubject(userId: UserId);

        // Act
        await contributor.IdentifyAsync(subject, TestContext.Current.CancellationToken);
        var export = new PersonalDataExport();
        await contributor.ExportAsync(new PersonalDataExportContext(subject, export), TestContext.Current.CancellationToken);
        var result = new PersonalDataAnonymizeResult();
        await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, null, result), TestContext.Current.CancellationToken);

        // Assert
        // The profile adds nothing to the person: a work email or phone may be shared with others.
        Assert.Empty(subject.Emails);
        Assert.Empty(subject.PhoneNumbers);

        var record = Assert.Single(export.Records);
        Assert.Equal(WorkerProfilePersonalDataContributor.WorkerProfileKind, record.Kind);
        Assert.Equal("ann@example.com", record.Fields[nameof(WorkerProfile.Email)]);

        Assert.Equal(1, result.Total);
        Assert.Equal("Former worker", worker.DisplayName);
        Assert.Null(worker.Email);
        Assert.Null(worker.Phone);
        Assert.Null(worker.DateOfBirth);
        Assert.Equal("E-7", worker.EmployeeNumber);
        workers.Verify(manager => manager.UpdateAsync(worker, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarketingPurchases_AreFoundThroughTheCustomerRecords_AndLoseTheEmail()
    {
        // Arrange
        var customers = new Mock<ICustomerService>();
        customers.Setup(service => service.FindForUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(new Customer { ItemId = "c-account" });

        var customerStore = new Mock<ICustomerStore>();
        customerStore.Setup(store => store.ListGuestsByEmailAsync("ann@example.com", It.IsAny<CancellationToken>())).ReturnsAsync([new Customer { ItemId = "c-guest" }]);

        var account = new CustomerPurchases { ItemId = "c-account", Email = "ann@example.com", Country = "US", OrderCount = 3, TotalSpent = 90m };
        var guest = new CustomerPurchases { ItemId = "c-guest", Email = "ann@example.com", Country = "US", OrderCount = 1, TotalSpent = 10m };

        var purchases = new Mock<ICustomerPurchasesStore>();
        purchases.Setup(store => store.FindByIdAsync("c-account", It.IsAny<CancellationToken>())).ReturnsAsync(account);
        purchases.Setup(store => store.FindByIdAsync("c-guest", It.IsAny<CancellationToken>())).ReturnsAsync(guest);

        var contributor = new MarketingPersonalDataContributor(customers.Object, customerStore.Object, purchases.Object);
        var subject = new PersonalDataSubject(userId: UserId, email: "ann@example.com");

        // Act
        var export = new PersonalDataExport();
        await contributor.ExportAsync(new PersonalDataExportContext(subject, export), TestContext.Current.CancellationToken);
        var result = new PersonalDataAnonymizeResult();
        await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, null, result), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["c-account", "c-guest"], export.Records.Select(record => record.Id).Order());
        Assert.Equal(2, result.Records[MarketingPersonalDataContributor.PurchasesKind]);
        Assert.Null(account.Email);
        Assert.Null(guest.Country);
        Assert.Equal(3, account.OrderCount);
        purchases.Verify(store => store.UpdateAsync(It.IsAny<CustomerPurchases>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ABusinessBuyer_IsExported_AndLosesTheirLimitAndPunchout()
    {
        // Arrange
        var account = TestB2BAccounts.Create(
            configure: part =>
            {
                part.OrderLimits = [new BuyerOrderLimit { UserId = UserId, Limit = 500m }, new BuyerOrderLimit { UserId = "user-2", Limit = 100m }];
                part.Punchout = new AccountPunchout { Enabled = true, UserId = UserId, KeyHash = "hash" };
            },
            members: [new AccountMember { UserId = UserId, UserName = "ann", Email = "ann@example.com", Role = AccountMemberRole.Member }]);

        var accounts = new Mock<IB2BAccountStore>();
        accounts.Setup(store => store.FindByBuyerAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(account);

        var contributor = new B2BBuyerPersonalDataContributor(accounts.Object);
        var subject = new PersonalDataSubject(userId: UserId);

        // Act
        var export = new PersonalDataExport();
        await contributor.ExportAsync(new PersonalDataExportContext(subject, export), TestContext.Current.CancellationToken);
        var result = new PersonalDataAnonymizeResult();
        await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, null, result), TestContext.Current.CancellationToken);

        // Assert
        var record = Assert.Single(export.Records);
        Assert.Equal("500", record.Fields[nameof(AccountBuyer.OrderLimit)]);
        Assert.Equal("True", record.Fields["PunchoutUser"]);

        Assert.Equal(1, result.Total);
        Assert.Equal(["user-2"], account.Part.OrderLimits.Select(limit => limit.UserId));
        Assert.False(account.Part.Punchout.Enabled);
        Assert.Null(account.Part.Punchout.UserId);
        Assert.Null(account.Part.Punchout.KeyHash);
        accounts.Verify(store => store.SaveAsync(account), Times.Once);
    }
}
