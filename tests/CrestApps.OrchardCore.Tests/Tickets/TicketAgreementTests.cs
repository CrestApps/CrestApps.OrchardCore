using System.ComponentModel.DataAnnotations;
using System.Reflection;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// A ticket's agreement: a new ticket of an account gets the agreement that covers it, whose service level runs the
/// ticket before the account's own; an agent changes or clears it, on the timeline, and an agreement that doesn't
/// exist, or is another account's, is refused; it is in the index for reports.
/// </summary>
public sealed class TicketAgreementTests
{
    [Fact]
    public async Task ANewTicket_GetsTheAgreementThatCoversIt_AndItsServiceLevel()
    {
        // Arrange (Contoso's support agreement covers the Support desk and brings the policy; the desk has none)
        await using var kit = await TicketKit.CreateAsync(withPolicy: true, configureDesk: desk => desk.ServiceLevelPolicyId = null, configureServices: services => services.AddSingleton<ITicketAgreementResolver>(new FakeResolver()));

        // Act
        var covered = await CreateAsync(kit, "contoso");
        var other = await CreateAsync(kit, "fabrikam");
        var timers = await kit.TimersAsync(covered.ContentItemId);
        var indexed = await kit.ReadAsync(async services => (await services.GetRequiredService<ISession>().QueryIndex<TicketIndex>(index => index.AgreementId != null).ListAsync()).Select(index => index.ContentItemId).ToList());

        // Assert
        Assert.Equal(FakeResolver.Gold, covered.GetOrCreate<TicketPart>().AgreementId);
        Assert.Null(other.GetOrCreate<TicketPart>().AgreementId);
        Assert.NotEmpty(timers);
        Assert.All(timers, timer => Assert.Equal(TicketKit.PolicyId, timer.PolicyId));
        Assert.Equal([covered.ContentItemId], indexed);
    }

    [Fact]
    public void ThePlanningPanelsNone_PostsAnEmptyAgreement_WhichClearsIt()
    {
        // Arrange (MVC turns an empty form value into null, and a null agreement means "leave it as it is")
        var property = typeof(TicketPlanningUpdateViewModel).GetProperty(nameof(TicketPlanningUpdateViewModel.AgreementId));

        // Act
        var format = property.GetCustomAttribute<DisplayFormatAttribute>();

        // Assert
        Assert.NotNull(format);
        Assert.False(format.ConvertEmptyStringToNull);
    }

    [Fact]
    public async Task AnAgent_ChangesOrClearsTheAgreement_OnTheTimeline_AndAnotherAccountsIsRefused()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync(withPolicy: true, configureDesk: desk => desk.ServiceLevelPolicyId = null, configureServices: services => services.AddSingleton<ITicketAgreementResolver>(new FakeResolver()));
        var ticketId = (await CreateAsync(kit, "contoso")).ContentItemId;

        // Act
        var toSilver = await UpdateAsync(kit, ticketId, new TicketUpdate { AgreementId = FakeResolver.Silver });
        var toOthers = await UpdateAsync(kit, ticketId, new TicketUpdate { AgreementId = FakeResolver.Fabrikam });
        var unknown = await UpdateAsync(kit, ticketId, new TicketUpdate { AgreementId = "gone" });
        var afterSilver = (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>().AgreementId;
        var cleared = await UpdateAsync(kit, ticketId, new TicketUpdate { AgreementId = string.Empty });
        var afterClear = (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>().AgreementId;
        var changes = (await kit.EntriesAsync(ticketId)).SelectMany(entry => entry.Changes).Where(change => change.Key == TicketChangeKeys.Agreement).ToList();

        // Assert
        Assert.True(toSilver.Succeeded && cleared.Succeeded);
        Assert.False(toOthers.Succeeded);
        Assert.False(unknown.Succeeded);
        Assert.Equal(FakeResolver.Silver, afterSilver);
        Assert.Null(afterClear);
        // Both entries carry the test clock's time, so their order on the timeline isn't what's checked.
        Assert.Equal(["Gold support → Silver support", "Silver support → "], changes.Select(change => change.From + " → " + change.To).Order(StringComparer.Ordinal));
    }

    private static async Task<ContentItem> CreateAsync(TicketKit kit, string accountId)
    {
        var result = await kit.RunAsync(services => services.GetRequiredService<ITicketService>().CreateAsync(new TicketCreateRequest
        {
            Subject = "VPN drops",
            Body = "Every hour.",
            DeskId = "support",
            AccountId = accountId,
            Channel = TicketsConstants.Channels.Agent,
        }, TestContext.Current.CancellationToken));

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));

        return result.Ticket;
    }

    private static Task<TicketResult> UpdateAsync(TicketKit kit, string ticketId, TicketUpdate update)
        => kit.RunAsync(services => services.GetRequiredService<ITicketService>().UpdateAsync(ticketId, update, TicketActor.User("ana", "Ana"), TestContext.Current.CancellationToken));

    private sealed class FakeResolver : ITicketAgreementResolver
    {
        public const string Gold = "gold";
        public const string Silver = "silver";
        public const string Fabrikam = "fabrikam-gold";

        private static readonly TicketAgreementInfo[] _agreements =
        [
            new(Gold, "SA-00001", "Gold support", "contoso", TicketKit.PolicyId),
            new(Silver, "SA-00002", "Silver support", "contoso", null),
            new(Fabrikam, "SA-00003", "Fabrikam support", "fabrikam-other", null),
        ];

        public Task<TicketAgreementInfo> FindForNewTicketAsync(string accountId, string deskId, string contentType, CancellationToken cancellationToken = default)
            => Task.FromResult(accountId == "contoso" && deskId == "support" ? _agreements[0] : null);

        public Task<TicketAgreementInfo> FindAsync(string agreementId, CancellationToken cancellationToken = default)
            => Task.FromResult(_agreements.FirstOrDefault(agreement => agreement.AgreementId == agreementId));

        public Task<IReadOnlyList<TicketAgreementInfo>> ListForAccountAsync(string accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TicketAgreementInfo>>([.. _agreements.Where(agreement => agreement.AccountId == accountId)]);
    }
}
