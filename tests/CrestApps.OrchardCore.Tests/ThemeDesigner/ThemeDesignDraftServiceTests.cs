using System.Security.Claims;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.SiteResources;
using CrestApps.OrchardCore.SiteResources.Models;
using CrestApps.OrchardCore.SiteResources.Services;
using CrestApps.OrchardCore.Tests.AgenticCommerce;
using CrestApps.OrchardCore.Tests.DataConnectors;
using CrestApps.OrchardCore.ThemeDesigner;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using CrestApps.OrchardCore.ThemeDesigner.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Cache;
using OrchardCore.Environment.Shell.Descriptor.Models;
using OrchardCore.Liquid;
using OrchardCore.ResourceManagement;
using OrchardCore.Security.Permissions;
using OrchardCore.Templates.Models;

namespace CrestApps.OrchardCore.Tests.ThemeDesigner;

public sealed class ThemeDesignDraftServiceTests
{
    private const string UserId = "designer-1";

    private const string CompleteLayout =
        """
        <html><head>{% resources type: "Meta" %}{% resources type: "HeadLink" %}{% resources type: "Stylesheet" %}{% resources type: "HeadScript" %}</head>
        <body>{% render_body %}{% resources type: "FootScript" %}</body></html>
        """;

    private readonly InMemoryDocumentManager<ThemeDesignDraftsDocument> _drafts = new();
    private readonly InMemoryDocumentManager<TemplatesDocument> _templates = new();
    private readonly InMemoryDocumentManager<AdminTemplatesDocument> _adminTemplates = new();
    private readonly InMemoryDocumentManager<DictionaryDocument<SiteResource>> _resources = new();
    private readonly Mock<INamedCatalogManager<SiteResource>> _resourceManager = new();
    private readonly Mock<ILiquidTemplateManager> _liquid = new();

    public ThemeDesignDraftServiceTests()
    {
        IEnumerable<string> noErrors = [];
        _liquid.Setup(liquid => liquid.Validate(It.IsAny<string>(), out noErrors)).Returns(true);
    }

    [Fact]
    public async Task SaveTemplateAsync_PutsTheTemplateInTheDraftWithoutPublishingIt()
    {
        // Arrange
        var service = CreateService(ThemeDesignerPermissions.ManageTemplates);

        // Act
        var result = await service.SaveTemplateAsync(ThemeDesignScope.Site, "Layout", CompleteLayout, "Main layout");

        // Assert
        Assert.True(result.Succeeded);
        var draft = await service.GetDraftAsync();
        var template = Assert.Single(draft.Templates);
        Assert.Equal("Layout", template.Name);
        Assert.Equal(ThemeDesignScope.Site, template.Scope);
        Assert.Null(_templates.Stored);
    }

    [Fact]
    public async Task SaveTemplateAsync_WhenTheLiquidDoesNotParse_KeepsTheDraftUnchanged()
    {
        // Arrange
        IEnumerable<string> errors = ["Unexpected end of tag."];
        _liquid.Setup(liquid => liquid.Validate(It.IsAny<string>(), out errors)).Returns(false);
        var service = CreateService(ThemeDesignerPermissions.ManageTemplates);

        // Act
        var result = await service.SaveTemplateAsync(ThemeDesignScope.Site, "Layout", "{% if %}", null);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("Unexpected end of tag."));
        Assert.Null(await service.GetDraftAsync());
    }

    [Fact]
    public async Task SaveTemplateAsync_WithoutThePermission_IsRefused()
    {
        // Arrange
        var service = CreateService(SiteResourcesPermissions.ManageSiteResources);

        // Act
        var result = await service.SaveTemplateAsync(ThemeDesignScope.Site, "Layout", "x", null);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(await service.GetDraftAsync());
    }

    [Fact]
    public async Task SaveTemplateAsync_AnAdminTemplateWhileAdminTemplatesAreOff_IsRefused()
    {
        // Arrange
        var service = CreateService(adminTemplatesEnabled: false, ThemeDesignerPermissions.ManageAdminTemplates);

        // Act
        var result = await service.SaveTemplateAsync(ThemeDesignScope.Admin, "Layout", "x", null);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task SaveTemplateAsync_ALayoutWithoutTheResourceTagsOrTheBody_IsRefused()
    {
        // Arrange
        var service = CreateService(ThemeDesignerPermissions.ManageTemplates);

        // Act
        var result = await service.SaveTemplateAsync(ThemeDesignScope.Site, "Layout", "<html><body>{% render_body %}</body></html>", null);

        // Assert
        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Errors);
        Assert.Contains("FootScript", error);
        Assert.DoesNotContain("render_body", error);
        Assert.Null(await service.GetDraftAsync());
    }

    [Fact]
    public void GetMissingLayoutParts_ACompleteLayout_MissesNothing()
        => Assert.Empty(ThemeDesignDraftService.GetMissingLayoutParts(CompleteLayout));

    [Theory]
    [InlineData("Layout", true)]
    [InlineData("layout__home", true)]
    [InlineData("Content", false)]
    [InlineData("LayoutWidget", false)]
    public void IsLayout_RecognizesTheLayoutAndItsAlternates(string name, bool expected)
        => Assert.Equal(expected, ThemeDesignDraftService.IsLayout(name));

    [Theory]
    [InlineData("Layout", true)]
    [InlineData("Content_Summary__BlogPost", true)]
    [InlineData("Content__Alias__about-us", true)]
    [InlineData("has space", false)]
    [InlineData("../Layout", false)]
    [InlineData("", false)]
    public void IsValidTemplateName_AcceptsShapeNamesOnly(string name, bool expected)
        => Assert.Equal(expected, ThemeDesignDraftService.IsValidTemplateName(name));

    [Fact]
    public async Task RemoveTemplateAsync_APublishedTemplate_IsMarkedForRemoval()
    {
        // Arrange
        await PublishTemplateAsync("Menu", "<ul></ul>");
        var service = CreateService(ThemeDesignerPermissions.ManageTemplates);

        // Act
        var result = await service.RemoveTemplateAsync(ThemeDesignScope.Site, "Menu");

        // Assert
        Assert.True(result.Succeeded);
        var change = Assert.Single((await service.GetChangesAsync()));
        Assert.Equal(ThemeDesignChangeStatus.Removed, change.Status);
    }

    [Fact]
    public async Task RemoveTemplateAsync_ATemplateOnlyTheDraftHas_DropsItFromTheDraft()
    {
        // Arrange
        var service = CreateService(ThemeDesignerPermissions.ManageTemplates);
        await service.SaveTemplateAsync(ThemeDesignScope.Site, "Footer", "<footer></footer>", null);

        // Act
        var result = await service.RemoveTemplateAsync(ThemeDesignScope.Site, "Footer");

        // Assert
        Assert.True(result.Succeeded);
        Assert.Null(await service.GetDraftAsync());
    }

    [Fact]
    public async Task RevertAsync_DropsTheChangeAndLeavesThePublishedTemplate()
    {
        // Arrange
        await PublishTemplateAsync("Layout", "published");
        var service = CreateService(ThemeDesignerPermissions.ManageTemplates);
        await service.SaveTemplateAsync(ThemeDesignScope.Site, "Layout", CompleteLayout, null);

        // Act
        var result = await service.RevertAsync(ThemeDesignChangeKind.SiteTemplate, "layout");

        // Assert
        Assert.True(result.Succeeded);
        Assert.Null(await service.GetDraftAsync());
        Assert.Equal("published", _templates.Stored.Templates["Layout"].Content);
    }

    [Fact]
    public async Task PublishAsync_AppliesTheTemplatesAndDeletesTheDraft()
    {
        // Arrange
        await PublishTemplateAsync("Menu", "old menu");
        var service = CreateService(ThemeDesignerPermissions.ManageTemplates);
        await service.SaveTemplateAsync(ThemeDesignScope.Site, "Layout", CompleteLayout, "Layout");
        await service.RemoveTemplateAsync(ThemeDesignScope.Site, "Menu");

        // Act
        var result = await service.PublishAsync();

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(CompleteLayout, _templates.Stored.Templates["Layout"].Content);
        Assert.False(_templates.Stored.Templates.ContainsKey("Menu"));
        Assert.Null(await service.GetDraftAsync());
    }

    [Fact]
    public async Task PublishAsync_WhenTheUserCanNoLongerChangeTheResources_PublishesNothing()
    {
        // Arrange
        var designer = CreateService(ThemeDesignerPermissions.ManageTemplates, SiteResourcesPermissions.ManageSiteResources);
        await designer.SaveTemplateAsync(ThemeDesignScope.Site, "Layout", CompleteLayout, null);
        await designer.SaveResourceAsync(new SiteResource { Name = "brand", Content = "body { color: red; }" });
        var restricted = CreateService(ThemeDesignerPermissions.ManageTemplates);

        // Act
        var result = await restricted.PublishAsync();

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(_templates.Stored);
        Assert.Equal(2, (await restricted.GetDraftAsync()).ChangeCount);
        _resourceManager.Verify(manager => manager.CreateAsync(It.IsAny<SiteResource>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveResourceAsync_MinifiesTheResourceInTheDraft()
    {
        // Arrange
        var service = CreateService(SiteResourcesPermissions.ManageSiteResources);

        // Act
        var result = await service.SaveResourceAsync(new SiteResource { Name = "brand", Content = "body {\n  color: blue;\n}" });

        // Assert
        Assert.True(result.Succeeded);
        var resource = (await service.GetResourceAsync("brand")).Draft;
        Assert.Equal("body{color:#00f}", resource.ServedContent);
        Assert.False(string.IsNullOrEmpty(resource.Version));
    }

    [Fact]
    public async Task SaveResourceAsync_AScriptThatDoesNotParse_IsRefused()
    {
        // Arrange
        var service = CreateService(SiteResourcesPermissions.ManageSiteResources);

        // Act
        var result = await service.SaveResourceAsync(new SiteResource { Name = "broken", Type = SiteResourceType.Script, Content = "function ( {" });

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(await service.GetDraftAsync());
    }

    [Fact]
    public async Task PublishAsync_CreatesTheDraftsNewResource()
    {
        // Arrange
        var service = CreateService(SiteResourcesPermissions.ManageSiteResources);
        await service.SaveResourceAsync(new SiteResource { Name = "brand", Content = "body { color: red; }", Target = SiteResourceTarget.Everywhere });
        _resourceManager.Setup(manager => manager.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new SiteResource { ItemId = "new-id" });
        _resourceManager.Setup(manager => manager.ValidateAsync(It.IsAny<SiteResource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResultDetails());

        // Act
        var result = await service.PublishAsync();

        // Assert
        Assert.True(result.Succeeded);
        _resourceManager.Verify(manager => manager.CreateAsync(
            It.Is<SiteResource>(resource => resource.ItemId == "new-id" && resource.Name == "brand" && resource.Target == SiteResourceTarget.Everywhere),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private async Task PublishTemplateAsync(string name, string content)
    {
        var document = await _templates.GetOrCreateMutableAsync(null);
        document.Templates[name] = new Template { Content = content };
        await _templates.UpdateAsync(document);
    }

    private ThemeDesignDraftService CreateService(params Permission[] permissions)
        => CreateService(adminTemplatesEnabled: true, permissions);

    private ThemeDesignDraftService CreateService(bool adminTemplatesEnabled, params Permission[] permissions)
    {
        var identity = new ClaimsIdentity(
            permissions.Select(permission => new Claim(Permission.ClaimType, permission.Name))
                .Append(new Claim(ClaimTypes.NameIdentifier, UserId))
                .Append(new Claim(ClaimTypes.Name, "designer")),
            "Test");

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };

        var shellDescriptor = new ShellDescriptor();

        if (adminTemplatesEnabled)
        {
            shellDescriptor.Features.Add(new ShellFeature(OrchardCoreFeatureIds.AdminTemplates));
        }

        var siteResourceService = new SiteResourceService(_resources, [], httpContextAccessor, NullLogger<SiteResourceService>.Instance);
        var validator = new SiteResourceValidator(_resources, Options.Create(new ResourceManagementOptions()), new PassThroughStringLocalizer<SiteResourceValidator>());

        return new ThemeDesignDraftService(
            httpContextAccessor,
            new PermissionClaimsAuthorizationService(),
            new ThemeDesignDraftStore(_drafts, Mock.Of<ITagCache>()),
            _templates,
            _adminTemplates,
            _resources,
            _resourceManager.Object,
            validator,
            siteResourceService,
            _liquid.Object,
            shellDescriptor,
            new FixedClock(),
            new PassThroughStringLocalizer<ThemeDesignDraftService>());
    }

    private sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
