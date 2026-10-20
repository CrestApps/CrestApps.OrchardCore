using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Migrations;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Locking;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Balances such as gift cards pay after tax, toward what's charged now, and are held, taken and put back through a
/// real YesSql store, so two checkouts can't spend the same money.
/// </summary>
public sealed class CheckoutTenderTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-tenders-{Guid.NewGuid():N}.db");
    private readonly LocalLock _lock = new(NullLogger<LocalLock>.Instance);
    private readonly FakeTenderProvider _provider = new();
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new CheckoutTenderHoldIndexProvider()]);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await new CheckoutTenderMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ABalance_PaysAfterTax_AndTheCardCarriesTheTax()
    {
        _provider.Balances["card-1"] = 30m;

        var invoice = await ApplyAsync(Invoice(), Session("session-a", "card-1"));

        var tender = Assert.Single(invoice.Tenders);
        Assert.Equal(30m, tender.Amount);
        Assert.Equal(0m, tender.TaxAmount);
        Assert.Equal(80m, invoice.InitialPaymentAmount);
        Assert.Equal(110m, invoice.GrandTotal);
        Assert.Equal(80m, invoice.AmountDue);
    }

    [Fact]
    public async Task ABalanceThatPaysNearlyEverything_TakesTheTaxTheCardCantCarry()
    {
        _provider.Balances["card-1"] = 105m;

        var invoice = await ApplyAsync(Invoice(), Session("session-a", "card-1"));

        Assert.Equal(105m, invoice.TenderTotal);
        Assert.Equal(5m, invoice.TenderTaxAmount);
        Assert.Equal(5m, invoice.InitialPaymentAmount);
    }

    [Fact]
    public async Task ABalanceLargerThanTheTotal_PaysEverything_AndLeavesNothingToCharge()
    {
        _provider.Balances["card-1"] = 500m;

        var invoice = await ApplyAsync(Invoice(), Session("session-a", "card-1"));

        Assert.Equal(110m, invoice.TenderTotal);
        Assert.Equal(0m, invoice.InitialPaymentAmount);
        Assert.Empty(CheckoutObligations.GetExpectedObligationIds(invoice));
    }

    [Fact]
    public async Task TwoBalances_PayInTheOrderTheyWereChosen()
    {
        _provider.Balances["card-1"] = 60m;
        _provider.Balances["card-2"] = 60m;

        var invoice = await ApplyAsync(Invoice(), Session("session-a", "card-2", "card-1"));

        Assert.Equal(["card-2", "card-1"], invoice.Tenders.Select(tender => tender.Reference));
        Assert.Equal(60m, invoice.Tenders[0].Amount);
        Assert.Equal(50m, invoice.Tenders[1].Amount);
    }

    [Fact]
    public async Task ACheckoutThatSettlesABalanceOrSavesACard_PaysByCardAlone()
    {
        _provider.Balances["card-1"] = 30m;

        var settling = Session("session-a", "card-1");
        settling.SettlesExistingBalance = true;

        var saving = Session("session-b", "card-1");
        saving.SavePaymentMethod = true;

        Assert.Empty((await ApplyAsync(Invoice(), settling)).Tenders);
        Assert.Empty((await ApplyAsync(Invoice(), saving)).Tenders);
    }

    [Fact]
    public async Task WhatOneCheckoutHolds_TheNextCantSpend_UntilItsLetGo()
    {
        _provider.Balances["card-1"] = 30m;

        var first = Session("session-a", "card-1");
        var invoice = await ApplyAsync(Invoice(), first);
        Assert.Equal(TenderAvailability.Available, await UseLedgerAsync(ledger => ledger.HoldAsync(new CheckoutFlow(first), invoice.Tenders[0], TestContext.Current.CancellationToken)));

        // The second checkout sees nothing left on the card, and can't hold what the first holds.
        var second = Session("session-b", "card-1");
        Assert.Empty((await ApplyAsync(Invoice(), second)).Tenders);
        Assert.Equal(TenderAvailability.Insufficient, await UseLedgerAsync(ledger => ledger.HoldAsync(new CheckoutFlow(second), new TenderLine { ProviderKey = FakeTenderProvider.ProviderKey, Reference = "card-1", Amount = 10m }, TestContext.Current.CancellationToken)));

        await UseLedgerAsync(async ledger =>
        {
            await ledger.ReleaseAsync("session-a", TestContext.Current.CancellationToken);

            return 0;
        });

        Assert.Equal(30m, (await ApplyAsync(Invoice(), second)).TenderTotal);
    }

    [Fact]
    public async Task CompletingTakesTheMoneyOnce_AndARefundPutsBackNoMoreThanWasTaken()
    {
        _provider.Balances["card-1"] = 30m;

        var session = Session("session-a", "card-1");
        var invoice = await ApplyAsync(Invoice(), session);
        var flow = new CheckoutFlow(session);

        await UseLedgerAsync(ledger => ledger.HoldAsync(flow, invoice.Tenders[0], TestContext.Current.CancellationToken));
        await UseLedgerAsync(async ledger =>
        {
            await ledger.RedeemAsync(flow, TestContext.Current.CancellationToken);
            await ledger.RedeemAsync(flow, TestContext.Current.CancellationToken);

            return 0;
        });

        Assert.Equal(0m, _provider.Balances["card-1"]);

        Assert.Equal(20m, await UseLedgerAsync(ledger => ledger.ReturnAsync("session-a", 20m, "Refund", TestContext.Current.CancellationToken)));
        Assert.Equal(10m, await UseLedgerAsync(ledger => ledger.ReturnAsync("session-a", 50m, "Refund", TestContext.Current.CancellationToken)));
        Assert.Equal(0m, await UseLedgerAsync(ledger => ledger.ReturnAsync("session-a", 50m, "Refund", TestContext.Current.CancellationToken)));
        Assert.Equal(30m, _provider.Balances["card-1"]);
    }

    private async Task<CheckoutInvoice> ApplyAsync(CheckoutInvoice invoice, CheckoutSession session)
    {
        await using var yesSql = _store.CreateSession();

        var ledger = new CheckoutTenderLedger(yesSql, _lock, [_provider], new TestClock(_now), NullLogger<CheckoutTenderLedger>.Instance);

        await new DefaultCheckoutTenderService([_provider], ledger, NullLogger<DefaultCheckoutTenderService>.Instance)
            .ApplyTendersAsync(invoice, new CheckoutFlow(session), TestContext.Current.CancellationToken);

        return invoice;
    }

    private async Task<T> UseLedgerAsync<T>(Func<ICheckoutTenderLedger, Task<T>> use)
    {
        await using var yesSql = _store.CreateSession();

        var result = await use(new CheckoutTenderLedger(yesSql, _lock, [_provider], new TestClock(_now), NullLogger<CheckoutTenderLedger>.Instance));

        await yesSql.SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
    }

    private static CheckoutSession Session(string sessionId, params string[] references)
    {
        var session = new CheckoutSession { SessionId = sessionId, Currency = "USD" };
        var selection = new CheckoutTenderSelection();

        foreach (var reference in references)
        {
            selection.Add(FakeTenderProvider.ProviderKey, reference);
        }

        session.Put(selection);

        return session;
    }

    // $100 of goods and $10 of tax, all charged now.
    private static CheckoutInvoice Invoice()
        => new()
        {
            Currency = "USD",
            InitialPaymentAmount = 110m,
            DueNow = 100m,
            TaxAmount = 10m,
            GrandTotal = 110m,
        };

    private sealed class FakeTenderProvider : ICheckoutTenderProvider
    {
        public const string ProviderKey = "fake";

        public Dictionary<string, decimal> Balances { get; } = [];

        public string Key
            => ProviderKey;

        public Task<IReadOnlyList<TenderLine>> GetTendersAsync(CheckoutTenderContext context, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TenderLine>>([.. context.References
                .Where(Balances.ContainsKey)
                .Select(reference => new TenderLine { Reference = reference, Description = "Card " + reference, Amount = Balances[reference] })]);

        public Task<decimal?> GetBalanceAsync(string reference, string currency, CancellationToken cancellationToken = default)
            => Task.FromResult<decimal?>(Balances.TryGetValue(reference, out var balance) ? balance : null);

        public Task DebitAsync(string reference, decimal amount, string currency, CheckoutFlow flow, CancellationToken cancellationToken = default)
        {
            Balances[reference] -= amount;

            return Task.CompletedTask;
        }

        public Task CreditAsync(string reference, decimal amount, string currency, string reason, CancellationToken cancellationToken = default)
        {
            Balances[reference] += amount;

            return Task.CompletedTask;
        }
    }
}
