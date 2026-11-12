using CrestApps.OrchardCore.Workforce.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;

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

    private static string Script(string userKey)
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

        return result.Content;
    }
}
