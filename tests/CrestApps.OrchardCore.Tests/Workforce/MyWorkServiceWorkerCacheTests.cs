using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Controllers;
using CrestApps.OrchardCore.Workforce.Portal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Users;
using OrchardCore.Users.Events;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// SEC-FE4 e: the My Work service worker kept every user's offline pages in one cache it never cleared, so on a shared
/// tablet that went offline the next user saw the previous worker's page.
/// </summary>
public sealed class MyWorkServiceWorkerCacheTests
{
    [Fact]
    public void ServiceWorker_NamesItsCachesForTheUser_AndDeletesOtherUsersCaches()
    {
        var key = MyWorkController.GetServiceWorkerUserKey("worker-1", "Default");

        var script = Script(key);

        Assert.Contains($"'my-work-pages-v2-{key}'", script);
        Assert.Contains($"'my-work-assets-v2-{key}'", script);
        Assert.Contains("caches.delete(key)", script);
    }

    [Fact]
    public void GetServiceWorkerUserKey_DiffersByUser_AndNeverHoldsTheUserId()
    {
        var first = MyWorkController.GetServiceWorkerUserKey("worker-1", "Default");
        var second = MyWorkController.GetServiceWorkerUserKey("worker-2", "Default");

        Assert.NotEqual(first, second);
        Assert.DoesNotContain("worker", first);
    }

    [Fact]
    public void ServiceWorker_IgnoresAKeyItDidNotMake()
    {
        var script = Script("x'); alert(1); ('");

        Assert.Contains("'my-work-pages-v2-anonymous'", script);
        Assert.DoesNotContain("alert(1)", script);
    }

    // SEC-FE4 e, the sign-out: the next user of a tablet that went offline was shown the last worker's page, because
    // nothing cleared the worker's caches when that worker signed out.
    [Fact]
    public void ServiceWorker_MarksTheDeviceAsOneThatKeepsMyWorkPages()
    {
        // Arrange & Act
        var (_, response) = Serve("worker-key");

        // Assert
        Assert.Contains(response.Headers.SetCookie, cookie => cookie.StartsWith(MyWorkSignOut.DeviceCookieName + "=1", StringComparison.Ordinal) && cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SigningOut_OnADeviceThatKeptMyWorkPages_ClearsTheSitesCachesAndStorage()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{MyWorkSignOut.DeviceCookieName}=1";
        var signOut = new MyWorkSignOut(new HttpContextAccessor { HttpContext = httpContext });

        // Act
        await signOut.LoggedOutAsync(Mock.Of<IUser>(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("\"cache\", \"storage\"", httpContext.Response.Headers["Clear-Site-Data"].ToString());
    }

    [Fact]
    public async Task SigningOut_OnADeviceThatNeverOpenedMyWork_LeavesItsStorageAlone()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var signOut = new MyWorkSignOut(new HttpContextAccessor { HttpContext = httpContext });

        // Act
        await signOut.LoggedOutAsync(Mock.Of<IUser>(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(httpContext.Response.Headers.ContainsKey("Clear-Site-Data"));
    }

    [Fact]
    public void ThePortal_RegistersTheSignOutHandler()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new PortalStartup(Mock.Of<IShellConfiguration>(configuration => configuration.GetSection(It.IsAny<string>()) == Mock.Of<IConfigurationSection>())).ConfigureServices(services);

        // Assert
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ILogoutFormEvent) && descriptor.ImplementationType == typeof(MyWorkSignOut));
    }

    private static string Script(string userKey)
        => Serve(userKey).Script;

    private static (string Script, HttpResponse Response) Serve(string userKey)
    {
        var localizer = new Mock<IStringLocalizer<MyWorkController>>();
        localizer.Setup(l => l[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));

        var controller = new MyWorkController(
            null, null, null, null, null, null, null, null, null, null, null, [], null, null, null, null,
            Mock.Of<IHtmlLocalizer<MyWorkController>>(),
            localizer.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() },
            },
        };

        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.Content(It.IsAny<string>())).Returns((string path) => path.TrimStart('~'));
        controller.Url = url.Object;

        var result = Assert.IsType<ContentResult>(controller.ServiceWorker(userKey));

        return (result.Content, controller.Response);
    }
}
