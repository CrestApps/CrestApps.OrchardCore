using System.Security.Claims;
using CrestApps.OrchardCore.Marketplace.Handlers;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.Tests.Marketplace;

/// <summary>
/// The vendor a product is sold for is who its sales' earnings are paid to. The editor lets only a marketplace manager
/// change it; the content API and imports went around the editor. The part's handler now holds every path to that rule.
/// </summary>
public sealed class VendorPartHandlerTests
{
    [Fact]
    public async Task AUserWhoDoesntManageTheMarketplace_CantChangeTheVendor_OutsideTheEditor()
    {
        // Arrange
        var (handler, item, part) = Create(managesMarketplace: false);

        await handler.LoadedAsync(new LoadContentContext(item), part);
        part.VendorId = "vendor-2";

        var context = new ValidateContentContext(item);

        // Act
        await handler.ValidatingAsync(context, part);

        // Assert
        Assert.False(context.ContentValidateResult.Succeeded);
    }

    [Fact]
    public async Task AMarketplaceManager_CanChangeTheVendor()
    {
        // Arrange
        var (handler, item, part) = Create(managesMarketplace: true);

        await handler.LoadedAsync(new LoadContentContext(item), part);
        part.VendorId = "vendor-2";

        var context = new ValidateContentContext(item);

        // Act
        await handler.ValidatingAsync(context, part);

        // Assert
        Assert.True(context.ContentValidateResult.Succeeded);
    }

    [Fact]
    public async Task AnEditThatLeavesTheVendor_IsNotRefused()
    {
        // Arrange
        var (handler, item, part) = Create(managesMarketplace: false);

        await handler.LoadedAsync(new LoadContentContext(item), part);

        var context = new ValidateContentContext(item);

        // Act
        await handler.ValidatingAsync(context, part);

        // Assert
        Assert.True(context.ContentValidateResult.Succeeded);
    }

    private static (VendorPartHandler Handler, ContentItem Item, VendorPart Part) Create(bool managesMarketplace)
    {
        var item = new ContentItem { ContentItemId = "product-1", ContentType = "Product" };
        var part = new VendorPart { VendorId = "vendor-1" };

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "test")),
        };

        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(managesMarketplace ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var handler = new VendorPartHandler(
            new HttpContextAccessor { HttpContext = httpContext },
            authorization.Object,
            new PassThroughStringLocalizer<VendorPartHandler>());

        return (handler, item, part);
    }
}
