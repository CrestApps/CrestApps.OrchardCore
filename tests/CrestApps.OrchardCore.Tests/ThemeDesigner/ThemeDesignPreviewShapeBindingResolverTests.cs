using System.Security.Claims;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Tests.AgenticCommerce;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using CrestApps.OrchardCore.ThemeDesigner.Storage;
using Microsoft.AspNetCore.Http;
using Moq;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Theming;
using OrchardCore.Environment.Cache;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.Tests.ThemeDesigner;

public sealed class ThemeDesignPreviewShapeBindingResolverTests
{
    private const string UserId = "designer-1";

    [Fact]
    public async Task GetShapeBindingAsync_AShapeTheDraftChanges_IsRenderedWithTheDraft()
    {
        // Arrange
        var resolver = CreateResolver(new ThemeDesignDraft
        {
            UserId = UserId,
            Templates = [new TemplateDraft { Scope = ThemeDesignScope.Site, Name = "Layout", Content = "<html></html>" }],
        });

        // Act
        var binding = await resolver.GetShapeBindingAsync("layout");

        // Assert
        Assert.NotNull(binding);
        Assert.Equal("ThemeDraft/layout.liquid", binding.BindingSource);
    }

    [Fact]
    public async Task GetShapeBindingAsync_AShapeTheDraftLeavesAlone_IsLeftToTheOtherResolvers()
    {
        // Arrange
        var resolver = CreateResolver(new ThemeDesignDraft
        {
            UserId = UserId,
            Templates = [new TemplateDraft { Scope = ThemeDesignScope.Site, Name = "Layout", Content = "<html></html>" }],
        });

        // Act & Assert
        Assert.Null(await resolver.GetShapeBindingAsync("Menu"));
    }

    [Fact]
    public async Task GetShapeBindingAsync_AnAdminTemplateOnAFrontEndPage_IsIgnored()
    {
        // Arrange
        var resolver = CreateResolver(new ThemeDesignDraft
        {
            UserId = UserId,
            Templates = [new TemplateDraft { Scope = ThemeDesignScope.Admin, Name = "Layout", Content = "<html></html>" }],
        });

        // Act & Assert
        Assert.Null(await resolver.GetShapeBindingAsync("Layout"));
    }

    [Fact]
    public async Task GetShapeBindingAsync_WhenThePreviewIsOff_ShowsThePublishedSite()
    {
        // Arrange
        var resolver = CreateResolver(new ThemeDesignDraft
        {
            UserId = UserId,
            Preview = false,
            Templates = [new TemplateDraft { Scope = ThemeDesignScope.Site, Name = "Layout", Content = "<html></html>" }],
        });

        // Act & Assert
        Assert.Null(await resolver.GetShapeBindingAsync("Layout"));
    }

    [Fact]
    public async Task GetShapeBindingAsync_ForAnAnonymousVisitor_ShowsThePublishedSite()
    {
        // Arrange
        var resolver = CreateResolver(
            new ThemeDesignDraft
            {
                UserId = UserId,
                Templates = [new TemplateDraft { Scope = ThemeDesignScope.Site, Name = "Layout", Content = "<html></html>" }],
            },
            signedIn: false);

        // Act & Assert
        Assert.Null(await resolver.GetShapeBindingAsync("Layout"));
    }

    // Synchronous on purpose: the HTTP context accessor keeps its context in an AsyncLocal, which an async helper would
    // not hand back to the test.
    private static ThemeDesignPreviewShapeBindingResolver CreateResolver(ThemeDesignDraft draft, bool signedIn = true)
    {
        var drafts = new InMemoryDocumentManager<ThemeDesignDraftsDocument>();
        var document = new ThemeDesignDraftsDocument();
        document.Drafts[draft.UserId] = draft;
        drafts.UpdateAsync(document).GetAwaiter().GetResult();

        var user = signedIn
            ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId)], "Test"))
            : new ClaimsPrincipal(new ClaimsIdentity());

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = user },
        };

        var preview = new ThemeDesignPreview(httpContextAccessor, new ThemeDesignDraftStore(drafts, Mock.Of<ITagCache>()));

        return new ThemeDesignPreviewShapeBindingResolver(
            preview,
            httpContextAccessor,
            Mock.Of<ILiquidTemplateManager>(),
            Mock.Of<IThemeManager>(),
            Mock.Of<IShapeTableManager>(),
            HtmlEncoder.Default);
    }
}
