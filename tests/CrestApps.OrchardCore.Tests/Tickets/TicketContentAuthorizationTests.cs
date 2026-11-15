using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Security;
using OrchardCore.Security.AuthorizationHandlers;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Orchard Core's own content permissions never open a ticket the ticket rules close. With the real authorization
/// service, Orchard Core's permission handler (which grants what a user's roles give) and the real ticket access
/// service: an editor with every content permission and no ticket permission can't view, edit or delete a ticket, an
/// anonymous visitor can't view one, an agent who sees the desk can view it but still can't edit or delete it through
/// the generic content pages, and a content item that isn't a ticket keeps Orchard Core's rules.
/// </summary>
public sealed class TicketContentAuthorizationTests
{
    private static readonly TicketDesk _hr = new() { ItemId = "hr", Name = "HR" };

    [Fact]
    public async Task AnEditorWithoutTicketRights_CantViewEditOrDeleteATicket()
    {
        // Arrange
        var authorization = Create();
        var editor = CalendarTestHarness.User("editor", CommonPermissions.ViewContent.Name, CommonPermissions.EditContent.Name, CommonPermissions.DeleteContent.Name, CommonPermissions.ListContent.Name);
        var ticket = Ticket();

        // Act and assert
        Assert.False(await authorization.AuthorizeAsync(editor, CommonPermissions.ViewContent, ticket));
        Assert.False(await authorization.AuthorizeAsync(editor, CommonPermissions.EditContent, ticket));
        Assert.False(await authorization.AuthorizeAsync(editor, CommonPermissions.DeleteContent, ticket));
        Assert.False(await authorization.AuthorizeAsync(editor, CommonPermissions.ListContent, ticket));
        Assert.True(await authorization.AuthorizeAsync(editor, CommonPermissions.ViewContent, new ContentItem { ContentItemId = "page", ContentType = "Page" }));
    }

    [Fact]
    public async Task AnAnonymousVisitor_CantViewATicket_EvenWithViewContent()
    {
        // Arrange
        var authorization = Create();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity([new Claim(Permission.ClaimType, CommonPermissions.ViewContent.Name)]));

        // Act and assert
        Assert.False(await authorization.AuthorizeAsync(anonymous, CommonPermissions.ViewContent, Ticket()));
    }

    [Fact]
    public async Task AnAgentOfTheDesk_CanViewTheTicket_ButNotChangeItThroughTheContentPages()
    {
        // Arrange
        var authorization = Create();
        var agent = CalendarTestHarness.User("agent", TicketsPermissions.ForDesk(TicketOperation.ViewAll, _hr).Name, TicketsPermissions.ForDesk(TicketOperation.Edit, _hr).Name, CommonPermissions.EditContent.Name);
        var ticket = Ticket();

        // Act and assert
        Assert.True(await authorization.AuthorizeAsync(agent, CommonPermissions.ViewContent, ticket));
        Assert.False(await authorization.AuthorizeAsync(agent, CommonPermissions.EditContent, ticket));
        Assert.False(await authorization.AuthorizeAsync(agent, new Permission("EditContent_Ticket", global::OrchardCore.Localization.LocalizationSource.Create<TicketContentAuthorizationTests>("Edit Ticket"), [CommonPermissions.EditContent]), ticket));
    }

    private static IAuthorizationService Create()
    {
        var desks = new Mock<ICatalog<TicketDesk>>();
        desks.Setup(catalog => catalog.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([_hr]);
        desks.Setup(catalog => catalog.FindByIdAsync("hr", It.IsAny<CancellationToken>())).ReturnsAsync(_hr);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddAuthorizationCore();
        services.AddSingleton<IPermissionGrantingService, DefaultPermissionGrantingService>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddScoped<IAuthorizationHandler, TicketContentAuthorizationHandler>();
        services.AddSingleton(desks.Object);
        services.AddScoped<ITicketAccessService, DefaultTicketAccessService>();

        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IAuthorizationService>();
    }

    private static ContentItem Ticket()
    {
        var item = new ContentItem { ContentItemId = "t1", ContentType = TicketsConstants.DefaultContentType, Owner = "editor", DisplayText = "Salary question" };
        item.Apply(new TicketPart { Number = "T-1", DeskId = "hr", StatusCategory = TicketStatusCategory.Open });

        return item;
    }
}
