using System.IO.Compression;
using System.Net;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The package content host on a running site: requests for another host name reach the same tenant, as a content
/// host pointed at the site would.
/// </summary>
public sealed partial class LearningPackagesTests
{
    private const string ContentHostName = "packages.test";

    [Fact]
    public async Task ContentHost_ServesTheFramePageAndPackageFiles_WithTheContentHostPolicy()
    {
        // Arrange
        var (courseId, itemId) = await CreatePackageCourseAsync(MultiFramePackage(), "multi-frame.zip", "learner1");
        var contentOrigin = $"http://{ContentHostName}:{_fixture.BaseAddress.Port}";
        var playerOrigin = $"http://127.0.0.1:{_fixture.BaseAddress.Port}";

        await SetContentHostAsync(contentOrigin);

        try
        {
            using var learner = await _fixture.CreateClientAsync("learner1");
            var player = await learner.GetStringAsync($"learning/courses/{courseId}/play/{itemId}", TestContext.Current.CancellationToken);
            var launch = await LaunchAsync(learner, itemId, player);
            var launchUrl = launch["launchUrl"]!.GetValue<string>();
            var token = launch["token"]!.GetValue<string>();
            var framePath = new Uri(launchUrl).PathAndQuery;

            // Act
            using var anonymous = await _fixture.CreateClientAsync();
            var frame = await GetOnHostAsync(anonymous, ContentHostName, framePath);
            var frameHtml = await frame.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            var driverPage = await GetOnHostAsync(anonymous, ContentHostName, $"/learning/packages/run/{token}/scormdriver/indexAPI.html");
            var driverHtml = await driverPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            var driverScript = await GetOnHostAsync(anonymous, ContentHostName, $"/learning/packages/run/{token}/scormdriver/scormdriver.js");
            var attachment = await GetOnHostAsync(anonymous, ContentHostName, $"/learning/packages/run/{token}/scormcontent/assets/attachment.docx");
            var tampered = await GetOnHostAsync(anonymous, ContentHostName, $"/learning/packages/run/{token[..^4]}AAAA/scormdriver/indexAPI.html");

            // Assert: the launch opens the frame page on the content host, with the frame's sandbox.
            Assert.StartsWith(contentOrigin + "/learning/packages/frame/", launchUrl);
            Assert.Equal(contentOrigin, launch["contentOrigin"]!.GetValue<string>());
            Assert.Equal(PackageContentHost.ContentHostFrameSandbox, launch["frameSandbox"]!.GetValue<string>());

            // The frame page holds the API in bridge mode and loads the launch file in a child frame.
            Assert.Equal(HttpStatusCode.OK, frame.StatusCode);
            AssertContentHostHeaders(frame, playerOrigin);
            Assert.Equal("no-store", frame.Headers.CacheControl?.ToString());
            Assert.Contains("window.CrestAppsScorm12Launch=", frameHtml);
            Assert.Contains("\"bridge\":true", frameHtml);
            Assert.Contains($"\"playerOrigin\":\"{playerOrigin}\"", frameHtml);
            Assert.Contains($"src=\"/learning/packages/run/{token}/scormdriver/indexAPI.html\"", frameHtml);

            // The package's own pages are served as they are: the frame page above them has the API.
            Assert.Equal(HttpStatusCode.OK, driverPage.StatusCode);
            AssertContentHostHeaders(driverPage, playerOrigin);
            Assert.Equal("no-store", driverPage.Headers.CacheControl?.ToString());
            Assert.DoesNotContain("CrestAppsScorm12Launch", driverHtml);
            Assert.Contains("<script src=\"scormdriver.js\"></script>", driverHtml);

            Assert.Equal(HttpStatusCode.OK, driverScript.StatusCode);
            Assert.Equal("text/javascript", driverScript.Content.Headers.ContentType?.MediaType);
            AssertContentHostHeaders(driverScript, playerOrigin);

            Assert.Equal(HttpStatusCode.OK, attachment.StatusCode);
            Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", attachment.Content.Headers.ContentType?.MediaType);

            Assert.Equal(HttpStatusCode.NotFound, tampered.StatusCode);
        }
        finally
        {
            await SetContentHostAsync(null);
        }
    }

    [Fact]
    public async Task ContentHost_RefusesPackageFilesOnTheSite_AndTheSiteOnTheContentHost()
    {
        // Arrange
        var (courseId, itemId) = await CreatePackageCourseAsync(MultiFramePackage(), "multi-frame.zip", "learner1");

        await SetContentHostAsync($"http://{ContentHostName}:{_fixture.BaseAddress.Port}");

        try
        {
            using var learner = await _fixture.CreateClientAsync("learner1");
            var player = await learner.GetStringAsync($"learning/courses/{courseId}/play/{itemId}", TestContext.Current.CancellationToken);
            var launch = await LaunchAsync(learner, itemId, player);
            var token = launch["token"]!.GetValue<string>();
            var runtimeUrl = launch["runtimeUrl"]!.GetValue<string>();

            // The admin's client carries the admin's cookie for the site's address on every request, whatever the Host.
            using var admin = await _fixture.CreateClientAsync("admin");

            // Act
            var fileOnSite = await learner.GetAsync($"learning/packages/run/{token}/scormdriver/indexAPI.html", TestContext.Current.CancellationToken);
            var frameOnSite = await learner.GetAsync($"learning/packages/frame/{token}", TestContext.Current.CancellationToken);
            var adminOnContentHost = await GetOnHostAsync(admin, ContentHostName, "/Admin");
            var homeOnContentHost = await GetOnHostAsync(admin, ContentHostName, "/");
            var loginOnContentHost = await GetOnHostAsync(admin, ContentHostName, "/Login");
            var playerOnContentHost = await GetOnHostAsync(learner, ContentHostName, $"/learning/courses/{courseId}/play/{itemId}");
            var runtimeOnContentHost = await GetOnHostAsync(learner, ContentHostName, runtimeUrl);
            var adminOnSite = await admin.GetAsync("Admin", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, fileOnSite.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, frameOnSite.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, adminOnContentHost.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, homeOnContentHost.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, loginOnContentHost.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, playerOnContentHost.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, runtimeOnContentHost.StatusCode);
            Assert.False(adminOnContentHost.Headers.Contains("Set-Cookie"));
            Assert.Equal(HttpStatusCode.OK, adminOnSite.StatusCode);
        }
        finally
        {
            await SetContentHostAsync(null);
        }
    }

    [Fact]
    public async Task StrictMode_HasNoFramePage_AndTheMultiFramePackageImports()
    {
        // Arrange
        var (courseId, itemId) = await CreatePackageCourseAsync(MultiFramePackage(), "multi-frame.zip", "learner1");
        using var learner = await _fixture.CreateClientAsync("learner1");
        var player = await learner.GetStringAsync($"learning/courses/{courseId}/play/{itemId}", TestContext.Current.CancellationToken);
        var launch = await LaunchAsync(learner, itemId, player);
        var token = launch["token"]!.GetValue<string>();

        // Act
        var frame = await learner.GetAsync($"learning/packages/frame/{token}", TestContext.Current.CancellationToken);
        var page = await learner.GetAsync(launch["launchUrl"]!.GetValue<string>().TrimStart('/'), TestContext.Current.CancellationToken);
        var cursor = await learner.GetAsync($"learning/packages/run/{token}/scormcontent/assets/cursor.cur", TestContext.Current.CancellationToken);

        // Assert: strict mode as before, with the new file kinds served.
        Assert.Null(launch["contentOrigin"]?.GetValue<string>());
        Assert.Equal(PackageContentHost.StrictFrameSandbox, launch["frameSandbox"]!.GetValue<string>());
        Assert.StartsWith("/learning/packages/run/", launch["launchUrl"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.NotFound, frame.StatusCode);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        AssertSandboxed(page);
        Assert.Equal(HttpStatusCode.OK, cursor.StatusCode);
        Assert.Equal("image/x-icon", cursor.Content.Headers.ContentType?.MediaType);
    }

    private static void AssertContentHostHeaders(HttpResponseMessage response, string playerOrigin)
    {
        Assert.Equal(
            $"sandbox allow-scripts allow-same-origin allow-forms allow-popups allow-popups-to-escape-sandbox; frame-ancestors 'self' {playerOrigin}",
            response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    }

    private static async Task<HttpResponseMessage> GetOnHostAsync(HttpClient client, string hostName, string pathAndQuery)
    {
        var address = new Uri(client.BaseAddress!, pathAndQuery.TrimStart('/'));
        using var request = new HttpRequestMessage(HttpMethod.Get, address);

        request.Headers.Host = $"{hostName}:{address.Port}";

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private Task SetContentHostAsync(string baseUrl)
        => _fixture.InTenantAsync(async services =>
        {
            var siteService = services.GetRequiredService<ISiteService>();
            var site = await siteService.LoadSiteSettingsAsync();

            site.Put(new LearningPackageContentHostSettings { BaseUrl = baseUrl });
            await siteService.UpdateSiteSettingsAsync(site);
        });

    // The synthetic Rise-like package of the test assets: a driver page framing the content, which calls the driver.
    private static byte[] MultiFramePackage()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "TestAssets", "MultiFramePackage");
        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                archive.CreateEntryFromFile(file, Path.GetRelativePath(folder, file).Replace('\\', '/'));
            }
        }

        return stream.ToArray();
    }
}
