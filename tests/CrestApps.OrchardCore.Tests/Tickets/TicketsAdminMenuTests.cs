using System.Security.Claims;
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
/// The help desk has one Tickets menu (the list and its own setup), and the schedules and policies other modules share
/// sit under Orchard Core's Settings menu; Orchard Core 4 has no Configuration menu.
/// </summary>
public sealed class TicketsAdminMenuTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheListAndTheHelpDeskSetup_AreOneTicketsMenu(bool canViewTickets)
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
        Assert.Equal("tickets", tickets.Id);
        string[] setup = ["ticketDesks", "ticketStatuses", "ticketPriorities", "ticketViews", "ticketEmails"];
        Assert.Equal(canViewTickets ? ["ticketList", .. setup] : setup, tickets.Items.OrderBy(item => item.Position).Select(item => item.Id));
    }

    [Fact]
    public async Task BusinessHoursAndServiceLevels_AreUnderSettings()
    {
        // Arrange
        var builder = new NavigationBuilder();

        // Act
        await new BusinessHoursAdminMenu(new PassThroughLocalizer<BusinessHoursAdminMenu>()).BuildNavigationAsync(NavigationConstants.AdminId, builder);
        await new ServiceLevelsAdminMenu(new PassThroughLocalizer<ServiceLevelsAdminMenu>()).BuildNavigationAsync(NavigationConstants.AdminId, builder);

        // Assert: each provider adds a Settings node (Orchard Core merges nodes of the same name into one menu).
        var roots = builder.Build();
        Assert.All(roots, root => Assert.Equal("Settings", root.Text.Value));
        Assert.Equal(["businessHoursSchedules", "serviceLevelPolicies"], roots.SelectMany(root => root.Items).Select(item => item.Id).Order());
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
