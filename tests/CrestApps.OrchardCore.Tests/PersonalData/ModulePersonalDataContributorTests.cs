using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Marketing.Services;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Tests.Commerce.B2B;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task AStaffRecord_IsExported_AndAnonymizedKeepingTheEmployeeNumber()
    {
        // Arrange
        await using var harness = await Workforce.WorkforceTests.CreateAsync();
        var staff = await Workforce.WorkforceTests.AddWorkerAsync(harness, "Ann Lee", member =>
        {
            member.UserId = UserId;
            member.Email = "ann@example.com";
            member.Phone = "+17025550100";
            member.EmployeeNumber = "E-7";
            member.DateOfBirth = new DateOnly(1990, 1, 2);
        });
        var subject = new PersonalDataSubject(userId: UserId);
        var export = new PersonalDataExport();
        var result = new PersonalDataAnonymizeResult();

        // Act
        await harness.RunAsync(async services =>
        {
            var contributor = new StaffPersonalDataContributor(services.GetRequiredService<StaffMemberStore>(), new PassThroughStringLocalizer<StaffPersonalDataContributor>());

            await contributor.IdentifyAsync(subject, TestContext.Current.CancellationToken);
            await contributor.ExportAsync(new PersonalDataExportContext(subject, export), TestContext.Current.CancellationToken);
            await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, null, result), TestContext.Current.CancellationToken);
        });
        var stored = await harness.RunAsync(services => services.GetRequiredService<StaffMemberStore>().FindByIdAsync(staff.ItemId).AsTask(), commit: false);

        // Assert
        // The record adds nothing to the person: a work email or phone may be shared with others.
        Assert.Empty(subject.Emails);
        Assert.Empty(subject.PhoneNumbers);

        var record = Assert.Single(export.Records);
        Assert.Equal(StaffPersonalDataContributor.Kind, record.Kind);
        Assert.Equal("ann@example.com", record.Fields[nameof(StaffMember.Email)]);

        Assert.Equal(1, result.Total);
        Assert.Equal("Former staff member", stored.DisplayName);
        Assert.Null(stored.Email);
        Assert.Null(stored.Phone);
        Assert.Null(stored.DateOfBirth);
        Assert.Equal("E-7", stored.EmployeeNumber);
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
