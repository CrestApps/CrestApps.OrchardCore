using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.SiteResources.Models;
using CrestApps.OrchardCore.SiteResources.Services;
using CrestApps.OrchardCore.Tests.AgenticCommerce;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.SiteResources;

public sealed class SiteResourceServiceTests
{
    [Fact]
    public void Minify_Stylesheet_RemovesCommentsAndWhitespace()
    {
        // Act
        var (served, errors) = SiteResourceService.Minify(SiteResourceType.Stylesheet, "/* brand */\nbody {\n    color: #ff0000;\n}\n", minify: true);

        // Assert
        Assert.Empty(errors);
        Assert.DoesNotContain("brand", served);
        Assert.DoesNotContain("\n", served);
        Assert.Contains("body{color:", served);
    }

    [Fact]
    public void Minify_ScriptWithSyntaxError_ServesItAsWrittenAndReportsTheError()
    {
        // Arrange
        const string Script = "function broken( { return 1; }";

        // Act
        var (served, errors) = SiteResourceService.Minify(SiteResourceType.Script, Script, minify: true);

        // Assert
        Assert.NotEmpty(errors);
        Assert.Equal(Script, served);
    }

    [Fact]
    public void Minify_WhenOff_ServesItAsWritten()
    {
        // Act
        var (served, errors) = SiteResourceService.Minify(SiteResourceType.Script, "let a = 1;   // keep", minify: false);

        // Assert
        Assert.Empty(errors);
        Assert.Equal("let a = 1;   // keep", served);
    }

    [Fact]
    public void Prepare_SetsTheServedContentAndAVersionThatFollowsIt()
    {
        // Arrange
        var service = CreateService(new InMemoryDocumentManager<DictionaryDocument<SiteResource>>());
        var resource = new SiteResource { Name = "brand", Content = "a { color: red; }" };

        // Act
        service.Prepare(resource);
        var firstVersion = resource.Version;
        resource.Content = "a { color: blue; }";
        service.Prepare(resource);

        // Assert
        Assert.Equal("a{color:#00f}", resource.ServedContent);
        Assert.Equal(12, firstVersion.Length);
        Assert.NotEqual(firstVersion, resource.Version);
    }

    [Fact]
    public void GetUrl_CarriesThePathBaseTheNameTheExtensionAndTheVersion()
    {
        // Arrange
        var resource = new SiteResource { Name = "brand", Type = SiteResourceType.Script, Version = "abc123" };

        // Act
        var url = SiteResourceService.GetUrl("/tenant1", "site-resources", resource);

        // Assert
        Assert.Equal("/tenant1/site-resources/brand.js?v=abc123", url);
    }

    [Fact]
    public async Task GetPageResourcesAsync_ReturnsTheEnabledResourcesOfThePagesSideInOrder()
    {
        // Arrange
        var documentManager = new InMemoryDocumentManager<DictionaryDocument<SiteResource>>();
        await documentManager.UpdateAsync(Document(
            new SiteResource { ItemId = "1", Name = "late", Position = 5, Target = SiteResourceTarget.Frontend },
            new SiteResource { ItemId = "2", Name = "early", Position = 1, Target = SiteResourceTarget.Everywhere },
            new SiteResource { ItemId = "3", Name = "admin-only", Target = SiteResourceTarget.Admin },
            new SiteResource { ItemId = "4", Name = "off", Target = SiteResourceTarget.Frontend, Enabled = false }));
        var service = CreateService(documentManager);

        // Act
        var frontEnd = await service.GetPageResourcesAsync(isAdmin: false, "TheBlogTheme");
        var admin = await service.GetPageResourcesAsync(isAdmin: true, "TheAdmin");

        // Assert
        Assert.Equal(["early", "late"], frontEnd.Select(entry => entry.Resource.Name));
        Assert.Equal(["admin-only", "early"], admin.Select(entry => entry.Resource.Name).Order());
        Assert.All(frontEnd, entry => Assert.StartsWith("/site-resources/", entry.Url));
    }

    [Fact]
    public async Task GetPageResourcesAsync_AResourceForSomeThemes_IsOfferedOnlyWhileOneOfThemRendersThePage()
    {
        // Arrange
        var documentManager = new InMemoryDocumentManager<DictionaryDocument<SiteResource>>();
        await documentManager.UpdateAsync(Document(
            new SiteResource { ItemId = "1", Name = "every-theme" },
            new SiteResource { ItemId = "2", Name = "blog-only", Themes = ["TheBlogTheme"] },
            new SiteResource { ItemId = "3", Name = "agency-only", Themes = ["theagencytheme"] }));
        var service = CreateService(documentManager);

        // Act
        var blog = await service.GetPageResourcesAsync(isAdmin: false, "TheBlogTheme");
        var agency = await service.GetPageResourcesAsync(isAdmin: false, "TheAgencyTheme");
        var unknown = await service.GetPageResourcesAsync(isAdmin: false, null);

        // Assert
        Assert.Equal(["blog-only", "every-theme"], blog.Select(entry => entry.Resource.Name).Order());
        Assert.Equal(["agency-only", "every-theme"], agency.Select(entry => entry.Resource.Name).Order());
        Assert.Equal(["every-theme"], unknown.Select(entry => entry.Resource.Name));
    }

    [Fact]
    public async Task GetPageResourcesAsync_LetsAHandlerReplaceAResource()
    {
        // Arrange
        var documentManager = new InMemoryDocumentManager<DictionaryDocument<SiteResource>>();
        await documentManager.UpdateAsync(Document(new SiteResource { ItemId = "1", Name = "brand" }));
        var draft = new SiteResource { ItemId = "1", Name = "brand", Content = "draft" };
        var service = CreateService(documentManager, new ReplacingHandler(draft, "/draft/brand.css"));

        // Act
        var entries = await service.GetPageResourcesAsync(isAdmin: false, "TheBlogTheme");

        // Assert
        var entry = Assert.Single(entries);
        Assert.Same(draft, entry.Resource);
        Assert.Equal("/draft/brand.css", entry.Url);
    }

    private static SiteResourceService CreateService(
        InMemoryDocumentManager<DictionaryDocument<SiteResource>> documentManager,
        params ISiteResourcesHandler[] handlers)
        => new(documentManager, handlers, new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, NullLogger<SiteResourceService>.Instance);

    private static DictionaryDocument<SiteResource> Document(params SiteResource[] resources)
    {
        var document = new DictionaryDocument<SiteResource>();

        foreach (var resource in resources)
        {
            document.Records[resource.ItemId] = resource;
        }

        return document;
    }

    private sealed class ReplacingHandler : ISiteResourcesHandler
    {
        private readonly SiteResource _resource;
        private readonly string _url;

        public ReplacingHandler(SiteResource resource, string url)
        {
            _resource = resource;
            _url = url;
        }

        public Task ResolvingAsync(SiteResourcesResolvingContext context)
        {
            context.Entries.RemoveAll(entry => entry.Resource.Name == _resource.Name);
            context.Entries.Add(new SiteResourceEntry(_resource, _url));

            return Task.CompletedTask;
        }
    }
}
