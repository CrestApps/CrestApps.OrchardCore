using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Controllers;
using CrestApps.OrchardCore.Workforce.Portal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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

    // SEC-FE4 e, second review: Clear-Site-Data clears the whole origin, so with tenants on paths of one host a sign-out
    // from one tenant wiped every tenant's storage, service workers and push registrations.
    [Theory]
    [InlineData("/t1/", "/t1/")]
    [InlineData("/t1/Admin/my-work", "/t1/Admin/my-work")]
    [InlineData("https://elsewhere.example/", "/t1/")]
    [InlineData("//elsewhere.example/", "/t1/")]
    public async Task SigningOut_OnADeviceThatRegisteredMyWork_GoesThroughThePageThatClearsOnlyMyWork(string redirect, string next)
    {
        // Arrange: the sign-out of a tenant on a path, as Orchard's LogOff runs it: the handler, then the redirect.
        var (httpContext, response) = SignOutContext();
        httpContext.Request.Headers.Cookie = $"{MyWorkSignOut.DeviceCookieName}=1";
        var signOut = SignOut(httpContext);

        // Act
        await signOut.LoggedOutAsync(Mock.Of<IUser>(), TestContext.Current.CancellationToken);
        httpContext.Response.Redirect(redirect);
        await response.StartAsync();

        // Assert
        Assert.False(httpContext.Response.Headers.ContainsKey("Clear-Site-Data"));
        Assert.Equal($"/t1{MyWorkSignedOutEndpoints.PagePath}?next={Uri.EscapeDataString(next)}&u={MyWorkController.GetServiceWorkerUserKey("worker-1", "Tenant1")}", httpContext.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task SigningOut_OnADeviceThatNeverOpenedMyWork_GoesWhereItWasGoing()
    {
        // Arrange
        var (httpContext, response) = SignOutContext();
        var signOut = SignOut(httpContext);

        // Act
        await signOut.LoggedOutAsync(Mock.Of<IUser>(), TestContext.Current.CancellationToken);
        httpContext.Response.Redirect("/t1/");
        await response.StartAsync();

        // Assert
        Assert.False(httpContext.Response.Headers.ContainsKey("Clear-Site-Data"));
        Assert.Equal("/t1/", httpContext.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task TheSignedOutPage_DeletesOnlyTheMyWorkCaches_AndThisTenantsServiceWorker_ThenGoesOnLocally()
    {
        // Arrange
        var httpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider() };
        httpContext.Request.PathBase = "/t1";
        var scriptContext = new DefaultHttpContext { RequestServices = httpContext.RequestServices };

        // Act
        var key = MyWorkController.GetServiceWorkerUserKey("worker-1", "Tenant1");
        var page = await RenderAsync(MyWorkSignedOutEndpoints.Page(httpContext, "https://elsewhere.example/", key), httpContext);
        var script = await RenderAsync(MyWorkSignedOutEndpoints.ScriptResult(scriptContext), scriptContext);

        // Assert
        Assert.Contains("src=\"/t1/my-work/signed-out.js\"", page, StringComparison.Ordinal);
        Assert.Contains("data-scope=\"/t1/Admin/my-work\"", page, StringComparison.Ordinal);
        Assert.Contains("data-next=\"/t1/\"", page, StringComparison.Ordinal);
        Assert.Contains($"data-user=\"{key}\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("elsewhere", page, StringComparison.Ordinal);
        Assert.Contains("key.startsWith('my-work-') && key.endsWith('-' + user)", script, StringComparison.Ordinal);
        Assert.Contains("registration.unregister()", script, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage", script, StringComparison.Ordinal);
        Assert.DoesNotContain("indexedDB", script, StringComparison.Ordinal);
    }

    private static (DefaultHttpContext HttpContext, StartingResponseFeature Response) SignOutContext()
    {
        var response = new StartingResponseFeature();
        var httpContext = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "worker-1")], "test")),
        };

        httpContext.Features.Set<IHttpResponseFeature>(response);
        httpContext.Request.PathBase = "/t1";

        return (httpContext, response);
    }

    private static MyWorkSignOut SignOut(HttpContext httpContext)
        => new(new HttpContextAccessor { HttpContext = httpContext }, new global::OrchardCore.Environment.Shell.ShellSettings { Name = "Tenant1" });

    private static async Task<string> RenderAsync(IResult result, DefaultHttpContext httpContext)
    {
        using var body = new MemoryStream();

        httpContext.Response.Body = body;
        await result.ExecuteAsync(httpContext);

        return System.Text.Encoding.UTF8.GetString(body.ToArray());
    }

    // A response that runs what was registered to happen as it starts, as the server does before it sends the headers.
    private sealed class StartingResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _starting = [];

        public override void OnStarting(Func<object, Task> callback, object state)
            => _starting.Add((callback, state));

        public async Task StartAsync()
        {
            foreach (var (callback, state) in Enumerable.Reverse(_starting))
            {
                await callback(state);
            }
        }
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
