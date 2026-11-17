using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Plaid;
using CrestApps.OrchardCore.Plaid.Models;
using CrestApps.OrchardCore.Plaid.Services;
using CrestApps.OrchardCore.Tests.Banking;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Plaid;

public sealed class PlaidWebhookInboxHandlerTests
{
    [Fact]
    public async Task PermissionRevoked_TakesTheLinkedAccountBackToUnverified_AndDropsTheItem()
    {
        // Arrange
        var (banking, plaid, handler, account) = await CreateAsync();

        // Act
        await handler.HandleAsync(Webhook("ITEM", "USER_PERMISSION_REVOKED", "item-1"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(BankAccountStatus.Unverified, account.Status);
        Assert.False(account.CanReceivePayments());
        Assert.Contains("Plaid", account.StatusReason, StringComparison.Ordinal);
        Assert.Empty(plaid.Store.Items);
        Assert.Single(plaid.Handler.CallsTo("item/remove"));
    }

    [Fact]
    public async Task AReplayedWebhook_DoesNothingMore()
    {
        // Arrange
        var (banking, plaid, handler, account) = await CreateAsync();
        var webhook = Webhook("ITEM", "USER_PERMISSION_REVOKED", "item-1");
        await handler.HandleAsync(webhook, TestContext.Current.CancellationToken);
        var changes = banking.Observer.Changes.Count;

        // Act
        await handler.HandleAsync(webhook, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(changes, banking.Observer.Changes.Count);
        Assert.Single(plaid.Handler.CallsTo("item/remove"));
    }

    [Fact]
    public async Task AccountRevoked_ForAnotherAccountOfTheItem_IsIgnored()
    {
        // Arrange
        var (_, plaid, handler, account) = await CreateAsync();

        // Act
        await handler.HandleAsync(Webhook("ITEM", "USER_ACCOUNT_REVOKED", "item-1", accountId: "acc-other"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(BankAccountStatus.Verified, account.Status);
        Assert.Single(plaid.Store.Items);
    }

    [Theory]
    [InlineData("ITEM", "PENDING_DISCONNECT")]
    [InlineData("ITEM", "ERROR")]
    [InlineData("AUTH", "AUTOMATICALLY_VERIFIED")]
    public async Task OtherWebhooks_AreIgnored(string type, string code)
    {
        // Arrange
        var (_, plaid, handler, account) = await CreateAsync();

        // Act
        await handler.HandleAsync(Webhook(type, code, "item-1"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(BankAccountStatus.Verified, account.Status);
        Assert.Single(plaid.Store.Items);
        Assert.Empty(plaid.Handler.Calls);
    }

    [Fact]
    public async Task AnAccountVerifiedSomeOtherWay_KeepsItsStatus_ButTheItemIsDropped()
    {
        // Arrange
        var (banking, plaid, handler, account) = await CreateAsync();
        account.VerificationMethod = "Prenote";

        // Act
        await handler.HandleAsync(Webhook("ITEM", "USER_PERMISSION_REVOKED", "item-1"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(BankAccountStatus.Verified, account.Status);
        Assert.Empty(plaid.Store.Items);
    }

    [Fact]
    public async Task Observer_ClosingTheAccountOrTypingOtherNumbers_DropsTheItem()
    {
        // Arrange
        var (banking, plaid, _, account) = await CreateAsync();
        var observer = new PlaidBankAccountObserver(plaid.Cleaner);

        // Act
        await observer.OnChangedAsync(new BankAccountChange { Account = account, Kinds = [BankAccountChangeKind.Updated] }, TestContext.Current.CancellationToken);
        var keptOnPlainUpdate = plaid.Store.Items.Count;
        await observer.OnChangedAsync(new BankAccountChange { Account = account, Kinds = [BankAccountChangeKind.NumberChanged] }, TestContext.Current.CancellationToken);
        var keptWhilePlaidStillTheMethod = plaid.Store.Items.Count;
        account.VerificationMethod = null;
        await observer.OnChangedAsync(new BankAccountChange { Account = account, Kinds = [BankAccountChangeKind.NumberChanged, BankAccountChangeKind.StatusChanged] }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, keptOnPlainUpdate);
        Assert.Equal(1, keptWhilePlaidStillTheMethod);
        Assert.Empty(plaid.Store.Items);
        Assert.Single(plaid.Handler.CallsTo("item/remove"));
    }

    private static async Task<(BankingTestContext Banking, PlaidTestContext Plaid, PlaidWebhookInboxHandler Handler, BankAccount Account)> CreateAsync()
    {
        var banking = new BankingTestContext();
        var plaid = new PlaidTestContext();
        var account = await banking.CreateAsync("9900009606");

        Assert.True(banking.Service.TrySetStatus(account, BankAccountStatus.Verified, verificationMethod: PlaidConstants.ProviderName));
        await banking.Manager.UpdateAsync(account, cancellationToken: TestContext.Current.CancellationToken);

        plaid.Store.Items.Add(new PlaidItem
        {
            ItemId = "item-1",
            PlaidAccountId = "acc-1",
            BankAccountId = account.ItemId,
            AccessToken = plaid.Protector.Protect(PlaidConstants.AccessTokenPurpose, "access-1", 0),
            Environment = PlaidEnvironment.Sandbox,
        });
        plaid.Handler.Answer("item/remove", "{}");

        var handler = new PlaidWebhookInboxHandler(
            plaid.Store,
            plaid.Cleaner,
            banking.Manager,
            banking.Service,
            NullLogger<PlaidWebhookInboxHandler>.Instance,
            RecipeRoundTrip.Localizer<PlaidWebhookInboxHandler>());

        return (banking, plaid, handler, account);
    }

    private static string Webhook(string type, string code, string itemId, string accountId = null)
        => $$"""{"webhook_type":"{{type}}","webhook_code":"{{code}}","item_id":"{{itemId}}","account_id":{{(accountId is null ? "null" : $"\"{accountId}\"")}},"environment":"sandbox"}""";
}
