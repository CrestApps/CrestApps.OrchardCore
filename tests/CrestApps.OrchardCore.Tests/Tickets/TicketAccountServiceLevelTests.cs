using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The Service level card of an account's page says which service level its tickets get: the policy of an agreement in
/// force that names one, for the tickets that agreement covers, and otherwise the account's own policy; with neither,
/// the tickets get their category's, type's or desk's.
/// </summary>
public sealed class TicketAccountServiceLevelTests
{
    [Fact]
    public async Task TheCard_ListsTheAgreementsPolicies_ThenTheAccountsOwn()
    {
        // Arrange: Northwind's own policy is Standard; its Gold agreement names Priority, its Silver agreement none.
        var account = Account(policyId: "standard");
        var agreements = new Mock<ITicketAgreementResolver>();
        agreements.Setup(value => value.ListForAccountAsync("acme", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TicketAgreementInfo("a1", "SA-00001", "Gold", "acme", "priority"), new TicketAgreementInfo("a2", "SA-00002", "Silver", "acme", null)]);
        var levels = new TicketAccountServiceLevels(Policies(), [agreements.Object]);

        // Act
        var card = await levels.DescribeAsync(account, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(("standard", "Standard"), (card.AccountPolicyId, card.AccountPolicyName));
        var agreement = Assert.Single(card.Agreements);
        Assert.Equal(("a1", "SA-00001", "Gold", "priority", "Priority"), (agreement.AgreementId, agreement.Number, agreement.Name, agreement.PolicyId, agreement.PolicyName));
    }

    [Fact]
    public async Task AnAccountWithNoPolicy_AndAgreementsOff_SaysSo()
    {
        // Arrange: no policy of its own (and one that names a removed policy reads as none); Agreements is off.
        var levels = new TicketAccountServiceLevels(Policies(), []);

        // Act
        var none = await levels.DescribeAsync(Account(policyId: null), TestContext.Current.CancellationToken);
        var removed = await levels.DescribeAsync(Account(policyId: "gone"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(none.AccountPolicyName);
        Assert.Empty(none.Agreements);
        Assert.Null(removed.AccountPolicyName);
    }

    private static IServiceLevelPolicyManager Policies()
    {
        var policies = new Mock<IServiceLevelPolicyManager>();
        policies.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => id switch
            {
                "standard" => new ServiceLevelPolicy { ItemId = "standard", Name = "Standard" },
                "priority" => new ServiceLevelPolicy { ItemId = "priority", Name = "Priority" },
                _ => null,
            });

        return policies.Object;
    }

    private static ContentItem Account(string policyId)
    {
        var account = new ContentItem { ContentItemId = "acme", ContentType = "Account", DisplayText = "Northwind" };
        account.Weld<AccountPart>();
        account.Apply(new TicketAccountPart { ServiceLevelPolicyId = policyId });

        return account;
    }
}
