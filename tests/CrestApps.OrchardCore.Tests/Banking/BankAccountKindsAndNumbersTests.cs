using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Banking;
using CrestApps.OrchardCore.Banking.Deployments;
using CrestApps.OrchardCore.Banking.Drivers;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Recipes;
using CrestApps.OrchardCore.Banking.ViewModels;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Tests.Disbursements;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Moq;

namespace CrestApps.OrchardCore.Tests.Banking;

/// <summary>
/// Account kinds beyond checking and savings, the business's own accounts saved without numbers, and accounts known
/// only by their last digits.
/// </summary>
public sealed class BankAccountKindsAndNumbersTests
{
    [Theory]
    [InlineData(BankAccountKind.Checking, true)]
    [InlineData(BankAccountKind.Savings, true)]
    [InlineData(BankAccountKind.CreditCard, false)]
    [InlineData(BankAccountKind.LineOfCredit, false)]
    [InlineData(BankAccountKind.Cash, false)]
    [InlineData(BankAccountKind.Other, false)]
    public void IsDepositAndCanDrawChecks_OnlyCheckingAndSavings(BankAccountKind kind, bool deposit)
    {
        // Act
        var isDeposit = BankAccountKinds.IsDeposit(kind);
        var canDrawChecks = BankAccountKinds.CanDrawChecks(kind);

        // Assert
        Assert.Equal(deposit, isDeposit);
        Assert.Equal(deposit, canDrawChecks);
    }

    [Fact]
    public void Kinds_AreAppendedAfterTheFirstTwo_SoStoredValuesKeepTheirMeaning()
    {
        // Assert
        Assert.Equal(0, (int)BankAccountKind.Checking);
        Assert.Equal(1, (int)BankAccountKind.Savings);
        Assert.Equal(2, (int)BankAccountKind.CreditCard);
        Assert.Equal(5, (int)BankAccountKind.Other);
    }

    [Fact]
    public async Task AdminEditor_ANewBusinessAccountWithoutNumbers_IsSaved()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.Manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        var posted = PostedFormUpdateModel.CreateContext(new BankAccountViewModel { Name = "Corporate card", HolderName = "Contoso", Kind = BankAccountKind.CreditCard, Country = "US", Currency = "USD" }, isNew: true);

        // Act
        await Driver(context).UpdateAsync(account, posted);

        // Assert
        Assert.True(posted.Updater.ModelState.IsValid);
        Assert.Equal(BankAccountKind.CreditCard, account.Kind);
        Assert.False(BankAccountNumbers.HasNumbers(account));
        Assert.True((await context.Manager.ValidateAsync(account, TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task AdminEditor_ANewPayeeAccountWithoutNumbers_IsStillRefused()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.Manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        var posted = PostedFormUpdateModel.CreateContext(new BankAccountViewModel { Name = "Pay", HolderName = "Pat", Owner = "Staff:1", Country = "US", Currency = "USD" }, isNew: true);

        // Act
        await Driver(context).UpdateAsync(account, posted);

        // Assert
        Assert.False(posted.Updater.ModelState.IsValid);
    }

    [Fact]
    public async Task AdminEditor_TheLastFourOfAnAccountWithoutItsNumber_AreKept_AndShownMasked()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.Manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        var posted = PostedFormUpdateModel.CreateContext(new BankAccountViewModel { Name = "Old checking", HolderName = "Contoso", Country = "US", Currency = "USD", AccountNumberLastFour = "4321" }, isNew: true);

        // Act
        await Driver(context).UpdateAsync(account, posted);

        // Assert
        Assert.True(posted.Updater.ModelState.IsValid);
        Assert.Equal("4321", account.AccountNumberLastFour);
        Assert.Equal("••••4321", BankAccountNumbers.Masked(account));
        Assert.Equal("4321", BankAccountNumbers.LastDigits(account));
        Assert.False(BankAccountNumbers.HasNumbers(account));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("12-3")]
    public async Task AdminEditor_LastFourThatAreNotOneToFourLettersOrDigits_AreRefused(string lastFour)
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.Manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        var posted = PostedFormUpdateModel.CreateContext(new BankAccountViewModel { Name = "Old checking", HolderName = "Contoso", Country = "US", Currency = "USD", AccountNumberLastFour = lastFour }, isNew: true);

        // Act
        await Driver(context).UpdateAsync(account, posted);

        // Assert
        Assert.False(posted.Updater.ModelState.IsValid);
        Assert.Null(account.AccountNumberLastFour);
    }

    [Fact]
    public async Task Validation_LastFourOfTheWrongForm_IsRefused()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.Manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        account.Name = "Old";
        account.HolderName = "Contoso";
        account.AccountNumberLastFour = "123456";

        // Act
        var result = await context.Manager.ValidateAsync(account, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ApplyNumbers_TheFullNumber_ReplacesTheLastFour()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.Manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        account.AccountNumberLastFour = "4321";

        // Act
        var errors = context.Service.ApplyNumbers(account, BankingTestContext.ValidRoutingNumber, "987654321");

        // Assert
        Assert.Empty(errors);
        Assert.Null(account.AccountNumberLastFour);
        Assert.Equal("••••4321", BankAccountNumbers.Masked(account));
    }

    [Fact]
    public async Task RoundTrip_CarriesTheKindAndTheLastFour_ButNeverAFullNumber()
    {
        // Arrange
        var source = new BankingTestContext();
        var account = await source.Manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        account.Name = "Petty cash";
        account.HolderName = "Contoso";
        account.Kind = BankAccountKind.Cash;
        await source.Manager.CreateAsync(account, TestContext.Current.CancellationToken);
        var known = await source.CreateAsync("555000111", name: "Operating");
        var step = await RecipeRoundTrip.ExportAsync(Source(source), new BankAccountsDeploymentStep());
        var target = new BankingTestContext();

        // Act
        var errors = await RecipeRoundTrip.RunAsync(new BankAccountsRecipeStep(target.Manager, target.Service), step);

        // Assert
        Assert.Empty(errors);
        Assert.DoesNotContain("555000111", step.ToJsonString(), StringComparison.Ordinal);
        Assert.Equal(BankAccountKind.Cash, target.Store.Entries.Single(entry => entry.Name == "Petty cash").Kind);
        var imported = target.Store.Entries.Single(entry => entry.ItemId == known.ItemId);
        Assert.Equal("0111", imported.AccountNumberLastFour);
        Assert.False(BankAccountNumbers.HasNumbers(imported));
    }

    [Fact]
    public async Task Recipe_AnAccountKnownByItsLastFour_IsImportedWithThem()
    {
        // Arrange
        var target = new BankingTestContext();
        var step = new JsonObject
        {
            ["name"] = BankingConstants.RecipeStepName,
            [BankAccountsRecipeStep.AccountsProperty] = new JsonArray(new JsonObject
            {
                ["Name"] = "Line of credit",
                ["HolderName"] = "Contoso",
                ["Kind"] = "LineOfCredit",
                ["AccountNumberLastFour"] = "7788",
            }),
        };

        // Act
        var errors = await RecipeRoundTrip.RunAsync(new BankAccountsRecipeStep(target.Manager, target.Service), step);

        // Assert
        Assert.Empty(errors);
        var imported = Assert.Single(target.Store.Entries);
        Assert.Equal(BankAccountKind.LineOfCredit, imported.Kind);
        Assert.Equal("7788", imported.AccountNumberLastFour);
    }

    [Fact]
    public async Task Disbursements_ABankFileFromOrToAnAccountThatIsNotCheckingOrSavings_IsRefused()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var payee = await context.PayeeAsync();
        var card = await context.Banking.CreateAsync("444455556666", name: "Corporate card");
        card.Kind = BankAccountKind.CreditCard;
        await context.Banking.Manager.UpdateAsync(card, cancellationToken: TestContext.Current.CancellationToken);
        payee.Kind = BankAccountKind.LineOfCredit;
        await context.Banking.Manager.UpdateAsync(payee, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var fromCard = await context.Service.CreateAsync(new DisbursementRequest { PayeeName = "Pat", Amount = 10, Method = "Check", SourceBankAccountId = card.ItemId, IdempotencyKey = "k1" }, TestContext.Current.CancellationToken);
        var toCredit = await context.Service.CreateAsync(new DisbursementRequest { PayeeName = "Pat", Amount = 10, Method = "Ach", SourceBankAccountId = origin.ItemId, DestinationBankAccountId = payee.ItemId, IdempotencyKey = "k2" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(fromCard.Succeeded);
        Assert.False(toCredit.Succeeded);
    }

    [Fact]
    public async Task Nacha_ABatchWhosePayeeAccountIsNoLongerADepositAccount_IsNotSent()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var payee = await context.PayeeAsync();
        var payment = await context.AchAsync(origin, payee);
        var (batch, _) = await context.Service.CreateBatchAsync("Nacha", [payment.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);
        payee.Kind = BankAccountKind.CreditCard;
        await context.Banking.Manager.UpdateAsync(payee, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var errors = await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(errors);
        Assert.False(batch.HasFile);
    }

    private static BankAccountDisplayDriver Driver(BankingTestContext context)
    {
        var directory = new Mock<IPartyDirectory>();

        directory.SetupGet(entry => entry.Kinds).Returns(["Staff"]);
        directory
            .Setup(entry => entry.GetNameAsync(It.IsAny<PartyRef>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PartyRef party, CancellationToken _) => party == new PartyRef("Staff", "1") ? "Pat Doe" : null);

        return new BankAccountDisplayDriver(
            context.Service,
            directory.Object,
            [],
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = TestSecurity.User("admin") } },
            RecipeRoundTrip.Localizer<BankAccountDisplayDriver>());
    }

    private static BankAccountsDeploymentSource Source(BankingTestContext context)
        => new(
            context.Manager,
            context.Service,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = TestSecurity.User("admin") } },
            TestSecurity.Granting(BankingPermissions.ManageBankAccounts),
            new RecordingNotifier(),
            new EchoHtmlLocalizer<BankAccountsDeploymentSource>(),
            RecipeRoundTrip.Localizer<BankAccountsDeploymentSource>());
}
