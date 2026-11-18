using System.Security.Claims;
using CrestApps.OrchardCore.Banking;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Parties.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Banking;

public sealed class BankAccountServiceTests
{
    [Fact]
    public async Task ApplyNumbers_StoresTheAccountNumberProtected_WithOnlyItsLastDigitsInClearText()
    {
        // Arrange
        var context = new BankingTestContext();

        // Act
        var account = await context.CreateAsync("9876543210");

        // Assert
        Assert.Equal(BankingTestContext.ValidRoutingNumber, account.RoutingNumber);
        Assert.Equal("3210", account.AccountNumber.Hint);
        Assert.DoesNotContain("9876543210", account.AccountNumber.Cipher, StringComparison.Ordinal);
        Assert.Equal("••••3210", BankAccountNumbers.Masked(account));
        Assert.Equal("9876543210", context.Protector.Unprotect(BankingConstants.AccountNumberPurpose, account.AccountNumber));
    }

    [Fact]
    public async Task ApplyNumbers_ARoutingNumberWithAWrongCheckDigit_IsRefusedAndNothingChanges()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.Manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var errors = context.Service.ApplyNumbers(account, "021000022", "123456789");

        // Assert
        var error = Assert.Single(errors);
        Assert.Equal(nameof(BankAccount.RoutingNumber), error.Field);
        Assert.Null(account.RoutingNumber);
        Assert.Null(account.AccountNumber);
    }

    [Fact]
    public async Task ApplyNumbers_ACountryWithoutItsOwnCheck_AcceptsLettersAndDigits()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.Manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        account.Country = "DE";

        // Act
        var errors = context.Service.ApplyNumbers(account, "37040044", "DE89 3704 0044 0532 0130 00");

        // Assert
        Assert.Empty(errors);
        Assert.Equal("37040044", account.RoutingNumber);
        Assert.Equal("3000", account.AccountNumber.Hint);
    }

    [Fact]
    public async Task ApplyNumbers_ANewNumberOnAVerifiedAccount_TakesItBackToUnverified_AndTellsTheObservers()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync("123456789");
        Assert.True(await context.Service.ChangeStatusAsync(account, BankAccountStatus.Verified, "Called the bank", cancellationToken: TestContext.Current.CancellationToken));
        context.Observer.Changes.Clear();

        // Act
        Assert.Empty(context.Service.ApplyNumbers(account, null, "555566667777"));
        await context.Manager.UpdateAsync(account, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(BankAccountStatus.Unverified, account.Status);
        Assert.Null(account.VerifiedUtc);
        Assert.False(account.CanReceivePayments());
        Assert.Equal(context.Now, account.NumberChangedUtc);

        var change = Assert.Single(context.Observer.Changes);
        Assert.True(change.Has(BankAccountChangeKind.NumberChanged));
        Assert.True(change.Has(BankAccountChangeKind.StatusChanged));
        Assert.Equal(BankAccountStatus.Verified, change.PreviousStatus);
    }

    [Fact]
    public async Task ApplyNumbers_TheSameNumberAgain_KeepsTheAccountVerified()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync("123456789");
        Assert.True(await context.Service.ChangeStatusAsync(account, BankAccountStatus.Verified, cancellationToken: TestContext.Current.CancellationToken));
        context.Observer.Changes.Clear();

        // Act
        Assert.Empty(context.Service.ApplyNumbers(account, BankingTestContext.ValidRoutingNumber, "1234-56789"));
        await context.Manager.UpdateAsync(account, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(BankAccountStatus.Verified, account.Status);
        Assert.Equal([BankAccountChangeKind.Updated], Assert.Single(context.Observer.Changes).Kinds);
    }

    [Fact]
    public async Task ApplyNumbers_ANewRoutingNumber_AlsoTakesTheAccountBackToUnverified()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync();
        Assert.True(await context.Service.ChangeStatusAsync(account, BankAccountStatus.Verified, cancellationToken: TestContext.Current.CancellationToken));

        // Act
        Assert.Empty(context.Service.ApplyNumbers(account, BankingTestContext.OtherValidRoutingNumber, null));

        // Assert
        Assert.Equal(BankAccountStatus.Unverified, account.Status);
        Assert.Equal(BankingTestContext.OtherValidRoutingNumber, account.RoutingNumber);
    }

    [Fact]
    public async Task ApplyNumbers_OnAClosedAccount_IsRefused()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync();
        Assert.True(await context.Service.ChangeStatusAsync(account, BankAccountStatus.Closed, cancellationToken: TestContext.Current.CancellationToken));

        // Act
        var errors = context.Service.ApplyNumbers(account, null, "99998888");

        // Assert
        Assert.Single(errors);
        Assert.Equal("6789", account.AccountNumber.Hint);
    }

    [Theory]
    [InlineData(BankAccountStatus.Closed, BankAccountStatus.Unverified)]
    [InlineData(BankAccountStatus.Closed, BankAccountStatus.Verified)]
    [InlineData(BankAccountStatus.Verified, BankAccountStatus.VerificationSent)]
    public async Task TrySetStatus_AMoveTheTableDoesNotAllow_IsRefused(BankAccountStatus from, BankAccountStatus to)
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync();
        Assert.True(context.Service.TrySetStatus(account, from));

        // Act
        var moved = context.Service.TrySetStatus(account, to);

        // Assert
        Assert.False(moved);
        Assert.Equal(from, account.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_ToVerified_StampsWhenAndHow_AndTellsTheObservers()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync();
        context.Observer.Changes.Clear();

        // Act
        var moved = await context.Service.ChangeStatusAsync(account, BankAccountStatus.Verified, "Called the bank", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(moved);
        Assert.Equal(context.Now, account.VerifiedUtc);
        Assert.Equal("Called the bank", account.StatusReason);
        Assert.True(account.CanReceivePayments());
        var change = Assert.Single(context.Observer.Changes);
        Assert.Equal([BankAccountChangeKind.StatusChanged], change.Kinds);
        Assert.Equal(BankAccountStatus.Unverified, change.PreviousStatus);
    }

    [Fact]
    public async Task CreateAsync_TellsTheObserversOnce_ThatTheAccountWasAdded()
    {
        // Arrange
        var context = new BankingTestContext();

        // Act
        var account = await context.CreateAsync();

        // Assert
        var change = Assert.Single(context.Observer.Changes);
        Assert.Equal([BankAccountChangeKind.Created], change.Kinds);
        Assert.Same(account, change.Account);
    }

    [Fact]
    public async Task RevealAccountNumberAsync_GivesTheNumber_AndTellsTheObserversWhoSawIt()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync("123456789");
        context.Observer.Changes.Clear();
        context.HttpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "clerk")], "test")),
        };

        // Act
        var number = await context.Service.RevealAccountNumberAsync(account, "Shown on the account page", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("123456789", number);
        var change = Assert.Single(context.Observer.Changes);
        Assert.Equal([BankAccountChangeKind.NumberRevealed], change.Kinds);
        Assert.Equal("clerk", change.Actor);
        Assert.Equal("Shown on the account page", change.Reason);
    }

    [Fact]
    public async Task StartVerificationAsync_MovesTheAccountToWhatTheVerifierSays()
    {
        // Arrange
        var context = new BankingTestContext([new FakeVerifier()]);
        var account = await context.CreateAsync();

        // Act
        var result = await context.Service.StartVerificationAsync(account, "Fake", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(BankAccountStatus.VerificationSent, account.Status);
        Assert.Equal("Fake", account.VerificationMethod);
    }

    [Fact]
    public async Task StartVerificationAsync_AnAccountWithoutNumbers_IsRefused()
    {
        // Arrange
        var context = new BankingTestContext([new FakeVerifier()]);
        var account = await context.Manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        account.Name = "Imported";
        account.HolderName = "Contoso Ltd";
        await context.Manager.CreateAsync(account, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var result = await context.Service.StartVerificationAsync(account, "Fake", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(BankAccountStatus.Unverified, account.Status);
    }

    [Fact]
    public async Task ListByOwnersAsync_ListsOnlyTheAccountsOfThoseOwners()
    {
        // Arrange
        var context = new BankingTestContext();
        var person = new PartyRef("Staff", "s1");
        await context.CreateAsync(owner: person, name: "Pay");
        await context.CreateAsync(owner: new PartyRef("Staff", "s2"), name: "Someone else");
        await context.CreateAsync(name: "Business");

        // Act
        var accounts = await context.Manager.ListByOwnersAsync([person], cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Pay", Assert.Single(accounts).Name);
    }

    [Fact]
    public async Task ValidateAsync_AnOwnerWithoutAnId_IsRefused()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.Manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        account.Name = "Pay";
        account.HolderName = "Pat";
        account.Owner = new PartyRef("Staff", null);

        // Act
        var result = await context.Manager.ValidateAsync(account, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    private sealed class FakeVerifier : IBankAccountVerifier
    {
        public string Name => "Fake";

        public LocalizedString DisplayName => new("Fake", "Fake");

        public Task<BankAccountVerificationResult> StartAsync(BankAccount account, CancellationToken cancellationToken = default)
            => Task.FromResult(BankAccountVerificationResult.Started());
    }
}
