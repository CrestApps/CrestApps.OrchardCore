using CrestApps.OrchardCore.Banking;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Tests.Banking;

public sealed class BankAccountLinkTests
{
    [Fact]
    public async Task ApplyLink_OnANewAccount_StoresTheBanksNumbersProtectedAndVerifiesIt()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.Manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var errors = context.Service.ApplyLink(account, "Linker", Linked("555666777"));

        // Assert
        Assert.Empty(errors);
        Assert.Equal(BankAccountStatus.Verified, account.Status);
        Assert.Equal("Linker", account.VerificationMethod);
        Assert.Equal(context.Now, account.VerifiedUtc);
        Assert.Equal(BankingTestContext.ValidRoutingNumber, account.RoutingNumber);
        Assert.Equal("555666777", context.Protector.Unprotect(BankingConstants.AccountNumberPurpose, account.AccountNumber));
        Assert.Equal(BankAccountKind.Savings, account.Kind);
        Assert.Equal("Linked Bank", account.BankName);
    }

    [Fact]
    public async Task ApplyLink_OnAnAccountWithOtherNumbers_RecordsTheChangedNumberAndVerifies()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync("123456789");

        // Act
        var errors = context.Service.ApplyLink(account, "Linker", Linked("999888777"));
        await context.Manager.UpdateAsync(account, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(errors);
        Assert.Equal(BankAccountStatus.Verified, account.Status);
        Assert.Equal("Linker", account.VerificationMethod);
        Assert.Equal("First Bank", account.BankName);

        var change = context.Observer.Changes[^1];
        Assert.True(change.Has(BankAccountChangeKind.NumberChanged));
        Assert.True(change.Has(BankAccountChangeKind.StatusChanged));
    }

    [Fact]
    public async Task ApplyLink_WithTheSameNumbersOnAVerifiedAccount_KeepsItVerifiedAndRecordsTheProvider()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync("555666777");
        Assert.True(context.Service.TrySetStatus(account, BankAccountStatus.Verified, "By hand"));

        // Act
        var errors = context.Service.ApplyLink(account, "Linker", Linked("555666777"));

        // Assert
        Assert.Empty(errors);
        Assert.Equal(BankAccountStatus.Verified, account.Status);
        Assert.Equal("Linker", account.VerificationMethod);
    }

    [Fact]
    public async Task ApplyLink_OnAClosedAccount_IsRefused()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync();
        Assert.True(context.Service.TrySetStatus(account, BankAccountStatus.Closed));

        // Act
        var errors = context.Service.ApplyLink(account, "Linker", Linked("555666777"));

        // Assert
        Assert.Single(errors);
        Assert.Equal(BankAccountStatus.Closed, account.Status);
    }

    [Fact]
    public async Task ApplyLink_WithoutNumbersOrWithARefusal_ChangesNothing()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.Manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        var noNumbers = Linked(null);

        // Act
        var refused = context.Service.ApplyLink(account, "Linker", BankAccountLinkResult.Refused("No"));
        var missing = context.Service.ApplyLink(account, "Linker", noNumbers);

        // Assert
        Assert.Equal("No", Assert.Single(refused).Message);
        Assert.Single(missing);
        Assert.Equal(BankAccountStatus.Unverified, account.Status);
        Assert.Null(account.AccountNumber);
    }

    [Fact]
    public async Task ApplyLink_WithAWrongRoutingNumber_IsRefusedLikeATypedOne()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.Manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
        var result = Linked("555666777");
        result.RoutingNumber = "021000022";

        // Act
        var errors = context.Service.ApplyLink(account, "Linker", result);

        // Assert
        Assert.Equal(nameof(BankAccount.RoutingNumber), Assert.Single(errors).Field);
        Assert.Equal(BankAccountStatus.Unverified, account.Status);
    }

    [Fact]
    public async Task LinkFlow_Complete_SavesANewAccountWithADefaultNameAndTellsTheProviderItWasSaved()
    {
        // Arrange
        var context = new BankingTestContext();
        var provider = new FakeLinkProvider(Linked("555666777"));
        var flow = Flow(context, provider);
        var linkContext = new BankAccountLinkContext { Account = await context.Manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken), IsNew = true, UserId = "u1" };

        // Act
        var errors = await flow.CompleteAsync(provider, linkContext, "{}", name: null, holderName: "Jane Doe", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(errors);
        var saved = Assert.Single(context.Store.Entries);
        Assert.Equal("Linked Bank ••••6777", saved.Name);
        Assert.Equal("Jane Doe", saved.HolderName);
        Assert.Equal(BankAccountStatus.Verified, saved.Status);
        Assert.True(provider.FinishedSaved);
    }

    [Fact]
    public async Task LinkFlow_Complete_WhenTheAccountIsNotValid_SavesNothingAndTellsTheProvider()
    {
        // Arrange
        var context = new BankingTestContext();
        var provider = new FakeLinkProvider(Linked("555666777"));
        var flow = Flow(context, provider);
        var linkContext = new BankAccountLinkContext { Account = await context.Manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken), IsNew = true, UserId = "u1" };

        // Act
        var errors = await flow.CompleteAsync(provider, linkContext, "{}", name: null, holderName: " ", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(errors);
        Assert.Empty(context.Store.Entries);
        Assert.False(provider.FinishedSaved);
        Assert.True(provider.Finished);
    }

    [Fact]
    public async Task LinkFlow_Complete_WhenTheProviderRefuses_SavesNothing()
    {
        // Arrange
        var context = new BankingTestContext();
        var provider = new FakeLinkProvider(BankAccountLinkResult.Refused("The bank said no."));
        var flow = Flow(context, provider);
        var linkContext = new BankAccountLinkContext { Account = await context.Manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken), IsNew = true, UserId = "u1" };

        // Act
        var errors = await flow.CompleteAsync(provider, linkContext, "{}", name: null, holderName: "Jane Doe", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("The bank said no.", Assert.Single(errors));
        Assert.Empty(context.Store.Entries);
    }

    [Fact]
    public async Task LinkFlow_FindProvider_SkipsAProviderThatIsNotAvailable()
    {
        // Arrange
        var provider = new FakeLinkProvider(Linked("1")) { Available = false };
        var context = new BankingTestContext(linkProviders: [provider]);
        var flow = Flow(context, provider);

        // Act
        var found = await flow.FindProviderAsync("linker", TestContext.Current.CancellationToken);
        var available = await flow.GetAvailableProvidersAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(found);
        Assert.Empty(available);
    }

    private static BankAccountLinkFlow Flow(BankingTestContext context, IBankAccountLinkProvider provider)
        => new(context.Service, context.Manager, Mock.Of<IShapeFactory>(), Mock.Of<IDisplayManager<global::CrestApps.OrchardCore.Banking.ViewModels.BankAccountLinkViewModel>>(), Mock.Of<global::OrchardCore.DisplayManagement.ModelBinding.IUpdateModelAccessor>(), RecipeRoundTrip.Localizer<BankAccountLinkFlow>());

    internal static BankAccountLinkResult Linked(string accountNumber)
        => new()
        {
            Succeeded = true,
            RoutingNumber = accountNumber is null ? null : BankingTestContext.ValidRoutingNumber,
            AccountNumber = accountNumber,
            Kind = BankAccountKind.Savings,
            BankName = "Linked Bank",
            Mask = "6777",
            Status = BankAccountStatus.Verified,
        };

    internal sealed class FakeLinkProvider : IBankAccountLinkProvider
    {
        private readonly BankAccountLinkResult _result;

        public FakeLinkProvider(BankAccountLinkResult result)
        {
            _result = result;
        }

        public bool Available { get; set; } = true;

        public bool Finished { get; private set; }

        public bool FinishedSaved { get; private set; }

        public string Name => "Linker";

        public LocalizedString DisplayName => new("Linker", "Linker");

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Available);

        public Task<BankAccountLinkStart> StartAsync(BankAccountLinkContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(BankAccountLinkStart.Ready("Shape", "payload"));

        public Task<BankAccountLinkResult> CompleteAsync(BankAccountLinkContext context, string clientResult, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);

        public Task FinishAsync(BankAccountLinkContext context, BankAccountLinkResult result, bool saved, CancellationToken cancellationToken = default)
        {
            Finished = true;
            FinishedSaved = saved;

            return Task.CompletedTask;
        }
    }
}
