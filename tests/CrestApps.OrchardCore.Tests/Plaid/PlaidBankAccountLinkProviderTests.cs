using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Plaid;
using CrestApps.OrchardCore.Plaid.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Plaid;

public sealed class PlaidBankAccountLinkProviderTests
{
    private const string ClientResult = """{"publicToken":"public-1","accountId":"acc-1","institutionName":"First Platypus Bank"}""";

    [Fact]
    public async Task Start_AsksPlaidForALinkTokenAndNamesTheShape()
    {
        // Arrange
        var (context, provider) = Create();
        context.Handler.Answer("link/token/create", """{"link_token":"link-sandbox-1"}""");

        // Act
        var start = await provider.StartAsync(LinkContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(start.Succeeded);
        Assert.Equal(PlaidConstants.LinkShapeType, start.ShapeType);
        Assert.Equal("link-sandbox-1", start.ClientPayload);
        Assert.Equal("hashed-user-1", Assert.Single(context.Handler.Calls).Body["user"]!["client_user_id"]!.GetValue<string>());
    }

    [Fact]
    public async Task Start_ForABankOutsideTheUnitedStates_IsRefusedWithoutAskingPlaid()
    {
        // Arrange
        var (context, provider) = Create();
        var link = LinkContext();
        link.Account.Country = "CA";

        // Act
        var start = await provider.StartAsync(link, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(start.Succeeded);
        Assert.Empty(context.Handler.Calls);
    }

    [Fact]
    public async Task IsAvailable_FollowsTheCredentials()
    {
        // Arrange
        var (context, provider) = Create();

        // Act
        var configured = await provider.IsAvailableAsync(TestContext.Current.CancellationToken);
        context.Credentials.Credentials = null;
        var notConfigured = await provider.IsAvailableAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(configured);
        Assert.False(notConfigured);
    }

    [Fact]
    public async Task CompleteAndFinish_WithARealNumber_ReturnsTheBanksNumbersVerified_AndRemovesTheItemAtOnce()
    {
        // Arrange
        var (context, provider) = Create();
        context.Handler
            .Answer("item/public_token/exchange", """{"access_token":"access-1","item_id":"item-1"}""")
            .Answer("auth/get", PlaidSamples.Auth("acc-1", "1111222233330000", "011401533", tokenized: false, subtype: "savings"))
            .Answer("item/remove", "{}");
        var link = LinkContext();

        // Act
        var result = await provider.CompleteAsync(link, ClientResult, TestContext.Current.CancellationToken);
        await provider.FinishAsync(link, result, saved: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("011401533", result.RoutingNumber);
        Assert.Equal("1111222233330000", result.AccountNumber);
        Assert.Equal(BankAccountKind.Savings, result.Kind);
        Assert.Equal("First Platypus Bank", result.BankName);
        Assert.Equal("0000", result.Mask);
        Assert.Equal(BankAccountStatus.Verified, result.Status);
        Assert.Equal("public-1", Assert.Single(context.Handler.CallsTo("item/public_token/exchange")).Body["public_token"]!.GetValue<string>());
        Assert.Equal("access-1", Assert.Single(context.Handler.CallsTo("item/remove")).Body["access_token"]!.GetValue<string>());
        Assert.Empty(context.Store.Items);
    }

    [Fact]
    public async Task CompleteAndFinish_WithATokenizedNumber_KeepsTheItemWithItsTokenProtected()
    {
        // Arrange
        var (context, provider) = Create();
        context.Handler
            .Answer("item/public_token/exchange", """{"access_token":"access-1","item_id":"item-1"}""")
            .Answer("auth/get", PlaidSamples.Auth("acc-1", "9900009606", "011401533", tokenized: true));
        var link = LinkContext();

        // Act
        var result = await provider.CompleteAsync(link, ClientResult, TestContext.Current.CancellationToken);
        await provider.FinishAsync(link, result, saved: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Empty(context.Handler.CallsTo("item/remove"));
        var item = Assert.Single(context.Store.Items);
        Assert.Equal("item-1", item.ItemId);
        Assert.Equal("acc-1", item.PlaidAccountId);
        Assert.Equal(link.Account.ItemId, item.BankAccountId);
        Assert.DoesNotContain("access-1", item.AccessToken.Cipher, StringComparison.Ordinal);
        Assert.True(string.IsNullOrEmpty(item.AccessToken.Hint));
        Assert.Equal("access-1", context.Protector.Unprotect(PlaidConstants.AccessTokenPurpose, item.AccessToken));
    }

    [Fact]
    public async Task Finish_WhenTheAccountWasNotSaved_RemovesTheItem()
    {
        // Arrange
        var (context, provider) = Create();
        context.Handler
            .Answer("item/public_token/exchange", """{"access_token":"access-1","item_id":"item-1"}""")
            .Answer("auth/get", PlaidSamples.Auth("acc-1", "9900009606", "011401533", tokenized: true))
            .Answer("item/remove", "{}");
        var link = LinkContext();

        // Act
        var result = await provider.CompleteAsync(link, ClientResult, TestContext.Current.CancellationToken);
        await provider.FinishAsync(link, result, saved: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(context.Handler.CallsTo("item/remove"));
        Assert.Empty(context.Store.Items);
    }

    [Fact]
    public async Task Finish_ANewLink_ReplacesWhatAnEarlierLinkKept()
    {
        // Arrange
        var (context, provider) = Create();
        var link = LinkContext();
        context.Store.Items.Add(new()
        {
            ItemId = "old-item",
            BankAccountId = link.Account.ItemId,
            AccessToken = context.Protector.Protect(PlaidConstants.AccessTokenPurpose, "old-access", 0),
        });
        context.Handler
            .Answer("item/public_token/exchange", """{"access_token":"access-1","item_id":"item-1"}""")
            .Answer("auth/get", PlaidSamples.Auth("acc-1", "1111222233330000", "011401533", tokenized: false))
            .Answer("item/remove", "{}");

        // Act
        var result = await provider.CompleteAsync(link, ClientResult, TestContext.Current.CancellationToken);
        await provider.FinishAsync(link, result, saved: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(context.Store.Items);
        Assert.Equal(["old-access", "access-1"], context.Handler.CallsTo("item/remove").Select(call => call.Body["access_token"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Complete_WhenTheBankHasNotConfirmedTheAccount_IsRefusedAndTheItemRemoved()
    {
        // Arrange
        var (context, provider) = Create();
        context.Handler
            .Answer("item/public_token/exchange", """{"access_token":"access-1","item_id":"item-1"}""")
            .Error("auth/get", "ITEM_ERROR", "PRODUCT_NOT_READY")
            .Answer("item/remove", "{}");

        // Act
        var result = await provider.CompleteAsync(LinkContext(), ClientResult, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("not confirmed", result.Message, StringComparison.Ordinal);
        Assert.Single(context.Handler.CallsTo("item/remove"));
    }

    [Fact]
    public async Task Complete_WhenPlaidRefusesTheExchange_ShowsPlaidsMessage()
    {
        // Arrange
        var (context, provider) = Create();
        context.Handler.Error("item/public_token/exchange", "INVALID_INPUT", "INVALID_PUBLIC_TOKEN", displayMessage: "Start again.");

        // Act
        var result = await provider.CompleteAsync(LinkContext(), ClientResult, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("Start again.", result.Message);
        Assert.Empty(context.Handler.CallsTo("item/remove"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"publicToken":"public-1"}""")]
    public async Task Complete_WithAMissingOrBrokenClientResult_IsRefusedWithoutAskingPlaid(string clientResult)
    {
        // Arrange
        var (context, provider) = Create();

        // Act
        var result = await provider.CompleteAsync(LinkContext(), clientResult, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(context.Handler.Calls);
    }

    private static (PlaidTestContext Context, PlaidBankAccountLinkProvider Provider) Create()
    {
        var context = new PlaidTestContext();
        var clock = new Mock<IClock>();
        clock.SetupGet(entry => entry.UtcNow).Returns(new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc));

        var provider = new PlaidBankAccountLinkProvider(
            context.Credentials,
            new FixedPlaidLinkSite(),
            context.Client,
            context.Store,
            context.Cleaner,
            context.Protector,
            clock.Object,
            NullLogger<PlaidBankAccountLinkProvider>.Instance,
            RecipeRoundTrip.Localizer<PlaidBankAccountLinkProvider>());

        return (context, provider);
    }

    private static BankAccountLinkContext LinkContext()
        => new()
        {
            Account = new BankAccount { ItemId = "bank-account-1", Country = "US" },
            IsNew = false,
            UserId = "user-1",
        };
}
