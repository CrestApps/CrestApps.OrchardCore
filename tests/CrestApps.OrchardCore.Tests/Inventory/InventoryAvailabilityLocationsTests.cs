using System.Reflection;
using System.Security.Claims;
using CrestApps.OrchardCore.Inventory.Api;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Inventory;

/// <summary>
/// SEC-C17m: the public availability endpoint lists only the store's own locations to shoppers. Stock kept in a van or
/// with a technician (a location another record owns) is staff information.
/// </summary>
public sealed class InventoryAvailabilityLocationsTests
{
    [Fact]
    public async Task GetAvailability_ForAGuest_LeavesOutLocationsOtherRecordsOwn()
    {
        // Arrange
        var result = await InvokeAsync(granted: ["ViewContent"]);

        // Assert
        Assert.Equal(["warehouse"], result.Select(location => location.LocationId));
    }

    [Fact]
    public async Task GetAvailability_ForInventoryStaff_ListsEveryLocation()
    {
        // Arrange
        var result = await InvokeAsync(granted: ["ViewContent", "ViewInventory"]);

        // Assert
        Assert.Equal(["warehouse", "van-1"], result.Select(location => location.LocationId));
    }

    private static async Task<IReadOnlyList<ApiLocationAvailability>> InvokeAsync(string[] granted)
    {
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync("mug", It.IsAny<VersionOptions>())).ReturnsAsync(new ContentItem { ContentItemId = "mug" });

        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => granted.Contains(requirement.Permission.Name))
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        var stock = new Mock<IStockItemStore>();
        stock.Setup(store => store.ListByProductAsync("mug", It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var locations = new Mock<IInventoryLocationService>();
        locations.Setup(service => service.GetLocationsAsync()).ReturnsAsync(
        [
            new InventoryLocation { LocationId = "warehouse", Name = "Warehouse" },
            new InventoryLocation { LocationId = "van-1", Name = "Van 1", OwnerKind = "Asset", OwnerId = "asset-1" },
        ]);

        var services = new ServiceCollection()
            .AddSingleton(contentManager.Object)
            .AddSingleton(authorization.Object)
            .AddSingleton(stock.Object)
            .AddSingleton(locations.Object)
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };
        var method = typeof(InventoryApiEndpoints).GetMethod("GetAvailabilityAsync", BindingFlags.NonPublic | BindingFlags.Static);
        var arguments = method.GetParameters()
            .Select(parameter => parameter.Name == "id" ? "mug"
                : parameter.Name == "variantId" ? null
                : parameter.ParameterType == typeof(HttpContext) ? httpContext
                : services.GetRequiredService(parameter.ParameterType))
            .ToArray();

        var task = (Task)method.Invoke(null, arguments);

        await task;

        var result = task.GetType().GetProperty("Result").GetValue(task);
        var inner = result.GetType().GetProperty("Result")?.GetValue(result) ?? result;

        return (IReadOnlyList<ApiLocationAvailability>)inner.GetType().GetProperty("Value").GetValue(inner);
    }
}
