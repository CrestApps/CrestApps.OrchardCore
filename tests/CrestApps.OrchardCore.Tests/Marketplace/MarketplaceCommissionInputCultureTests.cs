using System.Reflection;
using CrestApps.OrchardCore.Marketplace;
using CrestApps.OrchardCore.Marketplace.Controllers;
using CrestApps.OrchardCore.Marketplace.Drivers;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Services;
using CrestApps.OrchardCore.Tests.Core.ModelBinding;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Primitives;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Users.Services;
using MarketplaceStartup = CrestApps.OrchardCore.Marketplace.Startup;

namespace CrestApps.OrchardCore.Tests.Marketplace;

/// <summary>
/// Found with the fifth fix verification (outside the PR, on main from #1332): the store's and a vendor's commission
/// were read with <c>NumberStyles.Number</c> and the invariant culture, where the comma is a group separator, so a German
/// admin's "12,5" was refused or, as "1,5", read as 15. Each form is posted here through MVC's own binder, under the
/// request culture, into the real controller and settings driver.
/// </summary>
public sealed class MarketplaceCommissionInputCultureTests
{
    public static TheoryData<string, string> Posts => new()
    {
        { "en-US", "1.5" },
        { "de-DE", "1.5" },
        { "de-DE", "1,5" },
        { "fr-FR", "1,5" },
        { "nl-NL", "1.5" },
        { "nl-NL", "1,5" },
    };

    public static TheoryData<string, string> Unreadable => new()
    {
        { "en-US", "1,5.0" },
        { "de-DE", "1.5,0" },
        { "fr-FR", "douze" },
        { "de-DE", "150" },
    };

    [Theory]
    [MemberData(nameof(Posts))]
    public async Task Vendor_SavesTheCommissionTheFormSent(string culture, string posted)
    {
        var vendors = new Mock<IVendorStore>();
        var created = new List<Vendor>();
        vendors.Setup(store => store.CreateAsync(It.IsAny<Vendor>(), It.IsAny<CancellationToken>())).Callback((Vendor vendor, CancellationToken _) => created.Add(vendor));

        var result = await PostVendorAsync(vendors.Object, culture, posted);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1.5m, Assert.Single(created).CommissionPercent);
    }

    [Theory]
    [MemberData(nameof(Unreadable))]
    public async Task Vendor_WithACommissionThatIsNotAPercent_IsRefused(string culture, string posted)
    {
        var vendors = new Mock<IVendorStore>();

        var result = await PostVendorAsync(vendors.Object, culture, posted);

        Assert.IsType<ViewResult>(result);
        vendors.Verify(store => store.CreateAsync(It.IsAny<Vendor>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    // An empty commission still means the store's.
    [Fact]
    public async Task Vendor_WithAnEmptyCommission_HasTheStores()
    {
        var vendors = new Mock<IVendorStore>();
        var created = new List<Vendor>();
        vendors.Setup(store => store.CreateAsync(It.IsAny<Vendor>(), It.IsAny<CancellationToken>())).Callback((Vendor vendor, CancellationToken _) => created.Add(vendor));

        var result = await PostVendorAsync(vendors.Object, "de-DE", string.Empty);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Null(Assert.Single(created).CommissionPercent);
    }

    [Theory]
    [MemberData(nameof(Posts))]
    public async Task Settings_SaveTheCommissionTheFormSent(string culture, string posted)
    {
        var settings = new MarketplaceSettings { CommissionPercent = 10m };
        var updater = new FormPostUpdateModel(new MarketplaceStartup(), culture, new Dictionary<string, StringValues> { ["CommissionPercent"] = posted });

        await CreateSettingsDriver().UpdateAsync(Mock.Of<ISite>(), settings, updater.CreateContext());

        Assert.True(updater.ModelState.IsValid);
        Assert.Equal(1.5m, settings.CommissionPercent);
    }

    [Theory]
    [MemberData(nameof(Unreadable))]
    [InlineData("de-DE", "")]
    public async Task Settings_WithACommissionThatIsNotAPercent_AreRefused(string culture, string posted)
    {
        var settings = new MarketplaceSettings { CommissionPercent = 10m };
        var updater = new FormPostUpdateModel(new MarketplaceStartup(), culture, new Dictionary<string, StringValues> { ["CommissionPercent"] = posted });

        await CreateSettingsDriver().UpdateAsync(Mock.Of<ISite>(), settings, updater.CreateContext());

        Assert.False(updater.ModelState.IsValid);
        Assert.Equal(10m, settings.CommissionPercent);
    }

    private static async Task<IActionResult> PostVendorAsync(IVendorStore vendors, string culture, string commission)
    {
        var controller = new VendorsAdminController(
            vendors,
            Mock.Of<IVendorEarningStore>(),
            Mock.Of<IVendorPayoutStore>(),
            marketplace: null,
            Mock.Of<IUserService>(),
            TestSecurity.Granting(MarketplacePermissions.ManageMarketplace),
            Mock.Of<INotifier>(),
            Mock.Of<IShapeFactory>(),
            Mock.Of<global::OrchardCore.DisplayManagement.IDisplayManager<CrestApps.OrchardCore.Marketplace.ViewModels.VendorEditViewModel>>(),
            Mock.Of<global::OrchardCore.DisplayManagement.ModelBinding.IUpdateModelAccessor>(),
            Mock.Of<IClock>(),
            new EchoHtmlLocalizer<VendorsAdminController>(),
            new PassThroughStringLocalizer<VendorsAdminController>());

        var method = typeof(VendorsAdminController).GetMethod(nameof(VendorsAdminController.CreatePost), BindingFlags.Public | BindingFlags.Instance);
        var bound = await FormPostBinder.BindAsync(new MarketplaceStartup(), method, culture, new Dictionary<string, StringValues>
        {
            ["Name"] = "Acme Crafts",
            ["CommissionPercent"] = commission,
        }, controller: controller);

        controller.HttpContext.User = TestSecurity.User("admin");
        controller.TempData = Mock.Of<ITempDataDictionary>();

        return await (Task<IActionResult>)method.Invoke(controller, bound.Arguments);
    }

    private static MarketplaceSettingsDisplayDriver CreateSettingsDriver()
        => new(
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = TestSecurity.User("admin") } },
            TestSecurity.Granting(MarketplacePermissions.ManageMarketplace),
            [],
            new PassThroughStringLocalizer<MarketplaceSettingsDisplayDriver>());
}
