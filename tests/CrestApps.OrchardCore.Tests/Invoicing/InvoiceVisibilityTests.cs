using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// A customer sees the issued invoices billed to a party they have billing access to: themselves, or an account they are
/// an admin of. Never a draft, never someone else's, and never every invoice when they have access to no party.
/// </summary>
public sealed class InvoiceVisibilityTests
{
    private static readonly PartyRef _user = new(PartyKinds.User, "user-1");
    private static readonly PartyRef _account = new(PartyKinds.Account, "account-1");
    private static readonly PartyRef _otherAccount = new(PartyKinds.Account, "account-2");

    [Fact]
    public async Task TheCustomer_SeesTheIssuedInvoicesBilledToThemAndToTheirAccount_Only()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var mine = await IssueAsync(harness, _user);
        var account = await IssueAsync(harness, _account);
        await IssueAsync(harness, _otherAccount);
        await harness.CreateDraftAsync(invoice => invoice.BillTo.Party = _user);
        var visibility = CreateVisibility([_user, _account]);

        // Act
        var query = await visibility.CreateQueryAsync("user-1", TestContext.Current.CancellationToken);
        var seen = await harness.InScopeAsync(services => services.GetRequiredService<IInvoiceManager>().PageAsync(1, 50, query, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(new[] { account.ItemId, mine.ItemId }.Order(), seen.Entries.Select(invoice => invoice.ItemId).Order());
    }

    [Fact]
    public async Task ACustomerWithNoParty_SeesNoInvoice()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        await IssueAsync(harness, _account);
        var visibility = CreateVisibility([]);

        // Act
        var query = await visibility.CreateQueryAsync("user-1", TestContext.Current.CancellationToken);
        var seen = await harness.InScopeAsync(services => services.GetRequiredService<IInvoiceManager>().PageAsync(1, 50, query, TestContext.Current.CancellationToken));

        // Assert
        Assert.Empty(seen.Entries);
        Assert.Equal(0, seen.Count);
    }

    [Fact]
    public async Task OneInvoice_IsSeenOnlyWhenIssuedAndBilledToAPartyTheCustomerMayPayFor()
    {
        // Arrange
        var visibility = CreateVisibility([_account]);
        var issued = new Invoice { IssuedUtc = InvoicingTestHarness.Now, BillTo = new DocumentParty { Party = _account } };
        var draft = new Invoice { BillTo = new DocumentParty { Party = _account } };
        var others = new Invoice { IssuedUtc = InvoicingTestHarness.Now, BillTo = new DocumentParty { Party = _otherAccount } };
        var guest = new Invoice { IssuedUtc = InvoicingTestHarness.Now, BillTo = new DocumentParty { Party = new PartyRef(PartyKinds.Guest, "user-1") } };

        // Act and assert
        Assert.True(await visibility.CanViewAsync(issued, "user-1", TestContext.Current.CancellationToken));
        Assert.False(await visibility.CanViewAsync(draft, "user-1", TestContext.Current.CancellationToken));
        Assert.False(await visibility.CanViewAsync(others, "user-1", TestContext.Current.CancellationToken));
        Assert.False(await visibility.CanViewAsync(guest, "user-1", TestContext.Current.CancellationToken));
        Assert.False(await visibility.CanViewAsync(issued, null, TestContext.Current.CancellationToken));
    }

    // Membership with billing access to the given parties only.
    private static DefaultInvoiceVisibilityService CreateVisibility(PartyRef[] parties)
    {
        var membership = new Mock<IPartyMembershipService>();
        membership
            .Setup(service => service.GetPartiesAsync("user-1", PartyAccess.Billing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parties);
        membership
            .Setup(service => service.IsMemberAsync(It.IsAny<PartyRef>(), "user-1", PartyAccess.Billing, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PartyRef party, string _, PartyAccess _, CancellationToken _) => parties.Contains(party));

        return new DefaultInvoiceVisibilityService(membership.Object);
    }

    private static async Task<Invoice> IssueAsync(InvoicingTestHarness harness, PartyRef billTo)
    {
        var draft = await harness.CreateDraftAsync(invoice => invoice.BillTo.Party = billTo);
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.Invoice;
    }
}
