using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.WorkOrders.Integrations;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.WorkOrders;

/// <summary>
/// A work order with no account chosen takes the account of its contact, the list the contact is in, or else the
/// account its customer is a member of, through the Accounts module's members.
/// </summary>
public sealed class WorkOrderCustomersTests
{
    [Fact]
    public void AContactsAccount_IsTheListItIsIn()
    {
        var contact = new ContentItem { ContentItemId = "contact-1", ContentType = "Contact" };

        Assert.Null(WorkOrderCustomers.AccountOf(contact));

        contact.Content.ContainedPart = new JsonObject { ["ListContentItemId"] = "account-1" };

        Assert.Equal("account-1", WorkOrderCustomers.AccountOf(contact));
        Assert.Null(WorkOrderCustomers.AccountOf(null));
    }

    [Fact]
    public async Task ACustomerWhoIsAnAccountMember_GivesTheWorkOrderTheirAccount()
    {
        var members = new Mock<IAccountMemberService>();

        members.Setup(service => service.FindAccountAsync("user-1", It.IsAny<CancellationToken>())).ReturnsAsync(new ContentItem { ContentItemId = "account-1", ContentType = "Account" });

        var customers = new WorkOrderCustomers([], null, [members.Object]);

        Assert.Equal("account-1", await customers.AccountOfAsync(null, "user-1", TestContext.Current.CancellationToken));
        Assert.Null(await customers.AccountOfAsync(null, "user-2", TestContext.Current.CancellationToken));
        Assert.Null(await customers.AccountOfAsync(null, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WithoutAccountMembers_ACustomerGivesNoAccount()
    {
        var customers = new WorkOrderCustomers([], null, []);

        Assert.Null(await customers.AccountOfAsync(null, "user-1", TestContext.Current.CancellationToken));
        Assert.Null(await customers.AccountAddressAsync(null, TestContext.Current.CancellationToken));
    }
}
