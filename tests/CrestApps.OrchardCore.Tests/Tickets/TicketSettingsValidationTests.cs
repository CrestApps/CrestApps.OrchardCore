using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Handlers;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Recipes;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Ticket settings records are checked on save and on import alike: a badge color is a hex color or a color name and
/// nothing more; a saved view whose owner isn't a user of the site is refused with a reason; and a desk's permission
/// shows the desk's name in every tenant, even when two tenants have a desk with the same id.
/// </summary>
public sealed class TicketSettingsValidationTests
{
    [Theory]
    [InlineData("#1f6feb", true)]
    [InlineData("#abc", true)]
    [InlineData("#1f6feb80", true)]
    [InlineData("teal", true)]
    [InlineData("red; background-image: url(https://example.com/x)", false)]
    [InlineData("expression(alert(1))", false)]
    [InlineData("#12", false)]
    public async Task AStatusOrPriorityColor_IsAHexColorOrAName(string color, bool valid)
    {
        // Arrange
        var statusContext = new ValidatingContext<TicketStatus>(new TicketStatus { ItemId = "s1", Name = "Open", Color = color });
        var priorityContext = new ValidatingContext<TicketPriority>(new TicketPriority { ItemId = "p1", Name = "High", Color = color });

        // Act
        await Handler<TicketStatus>().ValidatingAsync(statusContext, TestContext.Current.CancellationToken);
        await Handler<TicketPriority>().ValidatingAsync(priorityContext, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(valid, statusContext.Result.Succeeded);
        Assert.Equal(valid, priorityContext.Result.Succeeded);
    }

    [Fact]
    public async Task AViewWhoseOwnerIsntAUserHere_IsRefusedWithAReason_AndASharedViewIsKept()
    {
        // Arrange
        var users = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        users.Setup(manager => manager.FindByIdAsync("ana-id")).ReturnsAsync(Mock.Of<IUser>());
        var services = new ServiceCollection().AddSingleton(users.Object).BuildServiceProvider();
        var known = new ValidatingContext<TicketView>(new TicketView { ItemId = "v1", Name = "Mine", OwnerId = "ana-id" });
        var unknown = new ValidatingContext<TicketView>(new TicketView { ItemId = "v2", Name = "Theirs", OwnerId = "4other-site-user" });
        var shared = new ValidatingContext<TicketView>(new TicketView { ItemId = "v3", Name = "Everyone" });

        // Act
        foreach (var context in new[] { known, unknown, shared })
        {
            await Handler<TicketView>(services).ValidatingAsync(context, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.True(known.Result.Succeeded);
        Assert.True(shared.Result.Succeeded);
        var error = Assert.Single(unknown.Result.Errors);
        Assert.Contains(nameof(TicketView.OwnerId), error.MemberNames);
        Assert.Contains("4other-site-user", error.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APersonalViewsOwner_TravelsByUserName_AndAnOwnerWhoIsntAUserHereIsRefused()
    {
        // Arrange
        var ana = Mock.Of<IUser>();
        var users = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        users.Setup(manager => manager.FindByNameAsync("ana")).ReturnsAsync(ana);
        users.Setup(manager => manager.FindByIdAsync("ana-here")).ReturnsAsync(ana);
        users.Setup(manager => manager.GetUserIdAsync(ana)).ReturnsAsync("ana-here");
        var services = new ServiceCollection().AddSingleton(users.Object).BuildServiceProvider();
        var known = new TicketView { ItemId = "v1", Name = "Mine" };
        var unknown = new TicketView { ItemId = "v2", Name = "Theirs" };
        var handler = Handler<TicketView>(services);

        // Act
        await handler.InitializingAsync(new InitializingContext<TicketView>(known, new JsonObject { ["Name"] = "Mine", [TicketView.OwnerUserNameProperty] = "ana" }), TestContext.Current.CancellationToken);
        await handler.InitializingAsync(new InitializingContext<TicketView>(unknown, new JsonObject { ["Name"] = "Theirs", [TicketView.OwnerUserNameProperty] = "bob" }), TestContext.Current.CancellationToken);
        var knownResult = new ValidatingContext<TicketView>(known);
        var unknownResult = new ValidatingContext<TicketView>(unknown);
        await handler.ValidatingAsync(knownResult, TestContext.Current.CancellationToken);
        await handler.ValidatingAsync(unknownResult, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("ana-here", known.OwnerId);
        Assert.True(knownResult.Result.Succeeded);
        Assert.Null(unknown.OwnerId);
        Assert.Contains("bob", Assert.Single(unknownResult.Result.Errors).ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ARecordWithAnIdThatIsntAUniqueId_IsRefusedByName_InsteadOfImportedUnderANewId()
    {
        // Arrange
        var context = new RecipeExecutionContext { Name = TicketsConstants.RecipeSteps.Desks };

        // Act
        var bad = TicketRecipeIds.CanImport(context.Name, new JsonObject { ["ItemId"] = "it-desk", ["Name"] = "IT" }, context);
        var good = TicketRecipeIds.CanImport(context.Name, new JsonObject { ["ItemId"] = "49vyvps9npqdzz9mz7xfwqgdzx", ["Name"] = "IT" }, context);
        var none = TicketRecipeIds.CanImport(context.Name, new JsonObject { ["Name"] = "IT" }, context);

        // Assert
        Assert.False(bad);
        Assert.True(good);
        Assert.True(none);
        var error = Assert.Single(context.Errors);
        Assert.Contains("it-desk", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ADesksPermission_FollowsItsName_EvenWhenAnotherTenantHasADeskWithTheSameId()
    {
        // Arrange
        var tenantA = new TicketDesk { ItemId = "4desk000000000000000000001", Name = "IT" };
        var tenantB = new TicketDesk { ItemId = "4desk000000000000000000001", Name = "Facilities" };

        // Act
        var a = TicketsPermissions.ForDesk(TicketOperation.Edit, tenantA);
        var b = TicketsPermissions.ForDesk(TicketOperation.Edit, tenantB);
        var again = TicketsPermissions.ForDesk(TicketOperation.Edit, tenantA);

        // Assert
        Assert.Equal(a.Name, b.Name);
        Assert.NotSame(a, b);
        Assert.Same(a, again);
        Assert.Contains("IT", a.Description.Value, StringComparison.Ordinal);
        Assert.Contains("Facilities", b.Description.Value, StringComparison.Ordinal);
    }

    private static TicketSettingsRecordHandler<T> Handler<T>(IServiceProvider services = null)
        where T : CrestApps.Core.Models.CatalogItem, ITicketSettingsRecord
    {
        var store = new Mock<ICatalog<T>>();
        store.Setup(catalog => catalog.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        return new TicketSettingsRecordHandler<T>(store.Object, Mock.Of<IClock>(), services ?? new ServiceCollection().BuildServiceProvider(), RecipeRoundTrip.Localizer<TicketSettingsRecordHandler<T>>());
    }
}
