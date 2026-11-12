using System.Security.Claims;
using CrestApps.OrchardCore.Classifieds.Drivers;
using CrestApps.OrchardCore.Classifieds.Models;
using CrestApps.OrchardCore.Classifieds.ViewModels;
using CrestApps.OrchardCore.Marketplace.Drivers;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Services;
using CrestApps.OrchardCore.Marketplace.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Admin;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Classifieds;

/// <summary>
/// SEC-C17o: a classified's status and featured dates are set only by someone who may moderate classifieds, and a
/// product's vendor only by someone who manages the marketplace; being on an admin page isn't enough.
/// </summary>
public sealed class ModerationFieldsPermissionTests
{
    [Fact]
    public async Task ClassifiedDriver_OnAnAdminPageWithoutModerate_KeepsTheStatus()
    {
        // Arrange
        var part = new ClassifiedPart { Status = ClassifiedStatus.Pending };
        var driver = Create<ClassifiedPartDisplayDriver>(authorized: false);

        // Act
        await driver.UpdateAsync(part, Context(nameof(ClassifiedPart), new ClassifiedPartViewModel { Status = (ClassifiedStatus)1 }));

        // Assert
        Assert.Equal(ClassifiedStatus.Pending, part.Status);
    }

    [Fact]
    public async Task ClassifiedDriver_ForAModerator_ChangesTheStatus()
    {
        // Arrange
        var part = new ClassifiedPart { Status = ClassifiedStatus.Pending };
        var driver = Create<ClassifiedPartDisplayDriver>(authorized: true);

        // Act
        await driver.UpdateAsync(part, Context(nameof(ClassifiedPart), new ClassifiedPartViewModel { Status = (ClassifiedStatus)1 }));

        // Assert
        Assert.Equal((ClassifiedStatus)1, part.Status);
    }

    [Fact]
    public async Task VendorDriver_WithoutManageMarketplace_KeepsTheVendor()
    {
        // Arrange
        var part = new VendorPart { VendorId = "vendor-a" };
        var driver = Create<VendorPartDisplayDriver>(authorized: false);

        // Act
        await driver.UpdateAsync(part, Context(nameof(VendorPart), new VendorPartViewModel { VendorId = "vendor-b" }));

        // Assert
        Assert.Equal("vendor-a", part.VendorId);
    }

    private static T Create<T>(bool authorized)
        where T : class
    {
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("test")) };

        AdminAttribute.Apply(httpContext);

        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var vendors = new Mock<IVendorStore>();
        vendors.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => new Vendor { ItemId = id });

        var services = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext })
            .AddSingleton(authorization.Object)
            .AddSingleton(vendors.Object)
            .AddSingleton(Mock.Of<IClock>())
            .AddSingleton(Mock.Of<ILocalClock>())
            .AddSingleton(typeof(IStringLocalizer<>), typeof(CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<>))
            .BuildServiceProvider();

        return ActivatorUtilities.CreateInstance<T>(services);
    }

    private static UpdatePartEditorContext Context(string partName, object posted)
        => new(new ContentTypePartDefinition(partName, new ContentPartDefinition(partName), []), PostedFormUpdateModel.CreateContext(posted));
}
