using System.Security.Claims;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.RealTime;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.SignalR;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Only a user who may see the tickets of at least one desk joins the boards' pings; the ping carries nothing about the
/// ticket, and each board reloads what its reader may see.
/// </summary>
public sealed class TicketBoardHubTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnlyAUserWithADeskViewScope_JoinsTheBoardsPings(bool hasScope)
    {
        // Arrange
        var access = new Mock<ITicketAccessService>();
        access.Setup(service => service.GetViewScopesAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasScope ? new Dictionary<string, TicketViewScope> { ["support"] = TicketViewScope.All } : new Dictionary<string, TicketViewScope>());
        var context = new Mock<HubCallerContext>();
        context.SetupGet(value => value.User).Returns(CalendarTestHarness.User("ana"));
        context.SetupGet(value => value.ConnectionId).Returns("c1");
        var groups = new Mock<IGroupManager>();
        var hub = new TicketBoardHub(access.Object, new ShellSettings { Name = "Default" }) { Context = context.Object, Groups = groups.Object };

        // Act
        var joined = await hub.Subscribe();

        // Assert
        Assert.Equal(hasScope, joined);
        groups.Verify(manager => manager.AddToGroupAsync("c1", TicketBoardHub.Group("Default"), It.IsAny<CancellationToken>()), hasScope ? Times.Once() : Times.Never());
    }
}
