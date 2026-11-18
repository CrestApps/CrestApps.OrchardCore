using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.PayLater.Models;
using CrestApps.OrchardCore.PayLater.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.PayLater;

/// <summary>
/// A Pay Later balance falls due the way an invoice does: under the payer's own payment term, then the term chosen for
/// Pay Later, at the end of the due day; with neither, or without the Payment Terms feature, the number of days applies.
/// </summary>
public sealed class PayLaterDueDateResolverTests
{
    private static readonly DateTime _start = new(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc);
    private static readonly PartyRef _account = new(PartyKinds.Account, "acme");

    private static readonly PaymentTerm _net15 = new() { ItemId = "net-15", Name = "Net 15", Kind = PaymentTermKind.NetDays, Days = 15 };
    private static readonly PaymentTerm _net60 = new() { ItemId = "net-60", Name = "Net 60", Kind = PaymentTermKind.NetDays, Days = 60 };
    private static readonly PaymentTerm _retired = new() { ItemId = "retired", Name = "Net 90", Kind = PaymentTermKind.NetDays, Days = 90, Enabled = false };

    [Fact]
    public async Task WithoutPaymentTerms_TheBalanceIsDueTheNumberOfDaysLater()
    {
        // Arrange
        var resolver = Create(new PayLaterSettings { NetTermDays = 30, PaymentTermId = "net-15" }, withTerms: false);

        // Act
        var due = await resolver.GetDueUtcAsync(_start, _account, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_start.AddDays(30), due);
    }

    [Fact]
    public async Task TheTermChosenForPayLater_ReplacesTheNumberOfDays_AtTheEndOfTheDueDay()
    {
        // Arrange
        var resolver = Create(new PayLaterSettings { NetTermDays = 30, PaymentTermId = "net-15" });

        // Act
        var due = await resolver.GetDueUtcAsync(_start, default, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new DateTime(2026, 11, 25, 23, 59, 59, DateTimeKind.Utc), due);
    }

    [Fact]
    public async Task ThePayersOwnTerm_WinsOverTheOneChosenForPayLater()
    {
        // Arrange: the account buys on Net 60.
        var resolver = Create(new PayLaterSettings { NetTermDays = 30, PaymentTermId = "net-15" }, profileTermId: "net-60");

        // Act
        var due = await resolver.GetDueUtcAsync(_start, _account, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new DateTime(2027, 1, 9, 23, 59, 59, DateTimeKind.Utc), due);
    }

    [Fact]
    public async Task ADisabledTerm_OrAGuest_FallsBackToWhatPayLaterUses()
    {
        // Arrange: the account's term was disabled, and a guest has no billing profile.
        var retired = Create(new PayLaterSettings { NetTermDays = 30, PaymentTermId = "net-15" }, profileTermId: "retired");
        var guest = Create(new PayLaterSettings { NetTermDays = 30 }, profileTermId: "net-60");

        // Act
        var forRetired = await retired.GetDueUtcAsync(_start, _account, TestContext.Current.CancellationToken);
        var forGuest = await guest.GetDueUtcAsync(_start, new PartyRef(PartyKinds.Guest, "session-1"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new DateTime(2026, 11, 25, 23, 59, 59, DateTimeKind.Utc), forRetired);
        Assert.Equal(_start.AddDays(30), forGuest);
    }

    private static PayLaterDueDateResolver Create(PayLaterSettings settings, bool withTerms = true, string profileTermId = null)
    {
        var services = new ServiceCollection();

        if (withTerms)
        {
            var terms = new[] { _net15, _net60, _retired };
            var manager = new Mock<IPaymentTermManager>();
            manager.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => terms.FirstOrDefault(term => term.ItemId == id));

            services.AddSingleton(manager.Object);
            services.AddSingleton<IPaymentTermCalculator, PaymentTermCalculator>();
        }

        var profiles = new Mock<IPartyBillingProfileService>();
        profiles.Setup(value => value.GetAsync(It.IsAny<PartyRef>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PartyBillingProfile { PaymentTermId = profileTermId });
        services.AddSingleton(profiles.Object);

        return new PayLaterDueDateResolver(SiteServiceFactory.Create(settings), services.BuildServiceProvider());
    }
}
