using System.Security.Claims;
using CrestApps.OrchardCore.Agreements.Navigation;
using CrestApps.OrchardCore.BusinessHours.Navigation;
using CrestApps.OrchardCore.ServiceLevels.Navigation;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Navigation;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The help desk has one Service Desk menu (the ticket list and its own setup). The records other modules share sit
/// under Content, like Teams: business hours schedules and agreements; service level policies sit under Tools.
/// Settings is for site settings, and Orchard Core 4 has no Configuration menu.
/// </summary>
public sealed class TicketsAdminMenuTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheListAndTheHelpDeskSetup_AreOneServiceDeskMenu(bool canViewTickets)
    {
        // Arrange
        var access = new Mock<ITicketAccessService>();
        access.Setup(value => value.GetViewScopesAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(canViewTickets
                ? new Dictionary<string, TicketViewScope> { ["support"] = TicketViewScope.All }
                : new Dictionary<string, TicketViewScope>());
        var httpContext = new Mock<IHttpContextAccessor>();
        httpContext.SetupGet(value => value.HttpContext).Returns(new DefaultHttpContext());
        var menu = new TicketsAdminMenu(access.Object, httpContext.Object, new PassThroughLocalizer<TicketsAdminMenu>());
        var builder = new NavigationBuilder();

        // Act
        await menu.BuildNavigationAsync(NavigationConstants.AdminId, builder);

        // Assert
        var tickets = Assert.Single(builder.Build());
        Assert.Equal("serviceDesk", tickets.Id);
        Assert.Equal("Service Desk", tickets.Text.Value);
        string[] setup = ["ticketDesks", "ticketStatuses", "ticketPriorities", "ticketViews", "ticketEmails"];
        Assert.Equal(canViewTickets ? ["ticketList", "ticketBoards", .. setup] : setup, tickets.Items.OrderBy(item => item.Position).Select(item => item.Id));
    }

    [Fact]
    public async Task BusinessHoursAndAgreements_AreUnderContent_AndServiceLevelsUnderTools()
    {
        // Arrange
        var builder = new NavigationBuilder();

        // Act
        await new BusinessHoursAdminMenu(new PassThroughLocalizer<BusinessHoursAdminMenu>()).BuildNavigationAsync(NavigationConstants.AdminId, builder);
        await new AgreementsAdminMenu(new PassThroughLocalizer<AgreementsAdminMenu>()).BuildNavigationAsync(NavigationConstants.AdminId, builder);
        await new ServiceLevelsAdminMenu(new PassThroughLocalizer<ServiceLevelsAdminMenu>()).BuildNavigationAsync(NavigationConstants.AdminId, builder);

        // Assert: each provider adds its parent node (Orchard Core merges nodes of the same name into one menu), and
        // none adds a top-level item of its own.
        var items = builder.Build().SelectMany(root => root.Items.Select(item => (Root: root.Text.Value, item.Id))).Order().ToList();
        Assert.Equal([("Content", "agreements"), ("Content", "businessHoursSchedules"), ("Tools", "serviceLevelPolicies")], items);
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
