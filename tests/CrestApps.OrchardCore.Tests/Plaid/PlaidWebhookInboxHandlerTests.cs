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
    public async Task EveryWebhook_IsHandedToEveryHandler_Once_WithoutCallingPlaid()
    {
        // Arrange
        var plaid = new PlaidTestContext();
        var first = new RecordingPlaidWebhookHandler();
        var second = new RecordingPlaidWebhookHandler();
        var third = new RecordingPlaidWebhookHandler();
        var handler = new PlaidWebhookInboxHandler([first, second, third], NullLogger<PlaidWebhookInboxHandler>.Instance);
        var payload = """{"webhook_type":"TRANSACTIONS","webhook_code":"SYNC_UPDATES_AVAILABLE","item_id":"item-9","initial_update_complete":true,"historical_update_complete":false,"environment":"sandbox"}""";

        // Act
        await handler.HandleAsync(payload, TestContext.Current.CancellationToken);

        // Assert
        foreach (var recorder in new[] { first, second, third })
        {
            var context = Assert.Single(recorder.Received);
            Assert.True(context.Is("TRANSACTIONS", "SYNC_UPDATES_AVAILABLE"));
            Assert.Equal("item-9", context.Webhook.ItemId);
            Assert.True(context.Webhook.InitialUpdateComplete);
            Assert.False(context.Webhook.HistoricalUpdateComplete);
            Assert.Equal(payload, context.Payload);
        }

        Assert.Empty(plaid.Handler.Calls);
    }

    [Fact]
    public async Task AFailingHandler_DoesNotStopTheOthers_AndTheFailureIsThrownSoTheInboxRetries()
    {
        // Arrange
        var failing = new RecordingPlaidWebhookHandler { Failure = new InvalidOperationException("boom") };
        var after = new RecordingPlaidWebhookHandler();
        var handler = new PlaidWebhookInboxHandler([failing, after], NullLogger<PlaidWebhookInboxHandler>.Instance);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(Webhook("ITEM", "ERROR", "item-1"), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("boom", exception.Message);
        Assert.Single(failing.Received);
        Assert.Single(after.Received);
    }

    [Fact]
    public async Task PermissionRevoked_ForAnItemLinkedToTwoBankAccounts_UnverifiesBoth_AndRemovesTheItemAtPlaidOnce()
    {
        // Arrange
        var (banking, plaid, handler, account) = await CreateAsync();
        var savings = await AddSecondAccountOfTheItemAsync(banking, plaid);

        // Act
        await handler.HandleAsync(Webhook("ITEM", "USER_PERMISSION_REVOKED", "item-1"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(BankAccountStatus.Unverified, account.Status);
        Assert.Equal(BankAccountStatus.Unverified, savings.Status);
        Assert.Empty(plaid.Store.Items);
        Assert.Single(plaid.Handler.CallsTo("item/remove"));
    }

    [Fact]
    public async Task AccountRevoked_ForOneAccountOfAnItemLinkedToTwoBankAccounts_KeepsTheItemForTheOther()
    {
        // Arrange
        var (banking, plaid, handler, account) = await CreateAsync();
        var savings = await AddSecondAccountOfTheItemAsync(banking, plaid);

        // Act
        await handler.HandleAsync(Webhook("ITEM", "USER_ACCOUNT_REVOKED", "item-1", accountId: "acc-2"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(BankAccountStatus.Verified, account.Status);
        Assert.Equal(BankAccountStatus.Unverified, savings.Status);
        var kept = Assert.Single(plaid.Store.Items);
        Assert.Equal(account.ItemId, kept.BankAccountId);
        Assert.Empty(plaid.Handler.CallsTo("item/remove"));
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
            [CreateBankAccountHandler(banking, plaid)],
            NullLogger<PlaidWebhookInboxHandler>.Instance);

        return (banking, plaid, handler, account);
    }

    private static async Task<BankAccount> AddSecondAccountOfTheItemAsync(BankingTestContext banking, PlaidTestContext plaid)
    {
        var savings = await banking.CreateAsync("9900009607");

        Assert.True(banking.Service.TrySetStatus(savings, BankAccountStatus.Verified, verificationMethod: PlaidConstants.ProviderName));
        await banking.Manager.UpdateAsync(savings, cancellationToken: TestContext.Current.CancellationToken);

        plaid.Store.Items.Add(new PlaidItem
        {
            ItemId = "item-1",
            PlaidAccountId = "acc-2",
            BankAccountId = savings.ItemId,
            AccessToken = plaid.Protector.Protect(PlaidConstants.AccessTokenPurpose, "access-1", 0),
            Environment = PlaidEnvironment.Sandbox,
        });

        return savings;
    }

    private static PlaidBankAccountWebhookHandler CreateBankAccountHandler(BankingTestContext banking, PlaidTestContext plaid)
        => new(
            plaid.Store,
            plaid.Cleaner,
            banking.Manager,
            banking.Service,
            RecipeRoundTrip.Localizer<PlaidBankAccountWebhookHandler>());

    private static string Webhook(string type, string code, string itemId, string accountId = null)
        => $$"""{"webhook_type":"{{type}}","webhook_code":"{{code}}","item_id":"{{itemId}}","account_id":{{(accountId is null ? "null" : $"\"{accountId}\"")}},"environment":"sandbox"}""";
}
