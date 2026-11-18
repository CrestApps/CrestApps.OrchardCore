using CrestApps.Core.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Handlers;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The ticket settings records stay consistent with the tickets that use them: a desk, status or priority that tickets
/// use reports that use (so it isn't deleted), and so does a policy a desk names; ticket targets and timers are keyed by
/// the priority's id, and a policy written with priority names is keyed by id when saved; a status's category and a
/// priority's rank reach the tickets that copied them.
/// </summary>
public sealed class TicketSettingsSqliteTests
{
    [Fact]
    public async Task ADeskStatusOrPriorityInUse_ReportsItsTickets_AndAPolicyReportsTheDeskThatNamesIt()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync(withPolicy: true);
        await kit.CreateAsync();

        // Act
        var usages = await kit.ReadAsync(async services =>
        {
            var usage = new TicketSettingsUsage(
                services.GetRequiredService<ISession>(),
                services.GetRequiredService<ICatalog<TicketDesk>>(),
                services.GetRequiredService<IContentDefinitionManager>(),
                Doubles.RecipeRoundTrip.Localizer<TicketSettingsUsage>());

            return new
            {
                Desk = await usage.GetUsagesAsync(new TicketDesk { ItemId = "support", Name = "Support" }, TestContext.Current.CancellationToken),
                OtherDesk = await usage.GetUsagesAsync(new TicketDesk { ItemId = "hr", Name = "HR" }, TestContext.Current.CancellationToken),
                Status = await usage.GetUsagesAsync(TicketKit.New, TestContext.Current.CancellationToken),
                Priority = await usage.GetUsagesAsync(TicketKit.Normal, TestContext.Current.CancellationToken),
                Policy = await usage.GetUsagesAsync(new ServiceLevelPolicy { ItemId = TicketKit.PolicyId }, TestContext.Current.CancellationToken),
            };
        });

        // Assert
        Assert.NotEmpty(usages.Desk);
        Assert.Empty(usages.OtherDesk);
        Assert.NotEmpty(usages.Status);
        Assert.NotEmpty(usages.Priority);
        Assert.Contains(usages.Policy, usage => usage.Contains("Support", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TimersAreKeyedByThePriorityId_AndAPolicyWrittenWithNamesIsKeyedByIdWhenSaved()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync(withPolicy: true);
        var priorities = new Mock<ICatalog<TicketPriority>>();
        priorities.Setup(catalog => catalog.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([TicketKit.Normal, TicketKit.Urgent]);
        var policy = new ServiceLevelPolicy
        {
            Scope = TicketsConstants.ServiceLevelScope,
            Targets = [new ServiceLevelTarget { Metric = ServiceLevelMetrics.FirstResponse, Priority = "urgent ", Minutes = 30 }, new ServiceLevelTarget { Metric = ServiceLevelMetrics.Resolution, Priority = "Normal", Minutes = 60 }],
        };

        // Act
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;
        var changed = await global::CrestApps.OrchardCore.ServiceLevels.Core.Services.ServiceLevelPriorityKeys.KeyByIdAsync(policy, new TicketServiceLevelScope(priorities.Object, Doubles.RecipeRoundTrip.Localizer<TicketServiceLevelScope>()), TestContext.Current.CancellationToken);

        // Assert
        Assert.All(await kit.TimersAsync(ticketId), timer => Assert.Equal(TicketKit.Normal.ItemId, timer.Priority));
        Assert.True(changed);
        Assert.Equal([TicketKit.Urgent.ItemId, TicketKit.Normal.ItemId], policy.Targets.Select(target => target.Priority));
    }

    [Fact]
    public async Task AStatusCategoryAndAPriorityRank_ReachTheTicketsThatCopiedThem()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync();
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;

        // The ticket's copies are out of date, as after its status and priority records were edited.
        await kit.RunAsync(async services =>
        {
            var contents = services.GetRequiredService<IContentManager>();
            var ticket = await contents.GetAsync(ticketId, VersionOptions.Latest);
            ticket.Alter<TicketPart>(part =>
            {
                part.StatusCategory = TicketStatusCategory.OnHold;
                part.PriorityRank = 9;
            });
            await contents.UpdateAsync(ticket);

            return true;
        });

        // Act
        var done = await kit.RunAsync(services => ActivatorUtilities.CreateInstance<TicketSettingsChangesStep>(services).RunAsync(new ResumableStepContext
        {
            Data = new Dictionary<string, string>
            {
                [TicketSettingsChangesStep.StatusPrefix + TicketKit.New.ItemId] = "0",
                [TicketSettingsChangesStep.PriorityPrefix + TicketKit.Normal.ItemId] = "0",
            },
        }, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(done);
        var part = (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>();
        Assert.Equal(TicketStatusCategory.New, part.StatusCategory);
        Assert.Equal(TicketKit.Normal.Rank, part.PriorityRank);
    }
}
