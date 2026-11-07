using System.Text.Json.Nodes;
using CrestApps.Core.AI.Memory;
using CrestApps.OrchardCore.NLWeb;
using CrestApps.OrchardCore.NLWeb.Models;
using CrestApps.OrchardCore.Seo.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Settings.Deployment;

namespace CrestApps.OrchardCore.Tests.Recipes;

/// <summary>
/// The structured data, NLWeb and AI memory settings travel between sites: each module registers a deployment step that
/// writes its settings into Orchard Core's <c>Settings</c> recipe step under the settings' name, and the written object
/// reads back into an empty site as the same settings.
/// </summary>
public sealed class SiteSettingsPortabilityTests
{
    [Fact]
    public async Task StructuredDataSettings_RoundTrip()
    {
        // Arrange
        var settings = new StructuredDataSettings
        {
            RenderOnPages = false,
            PublisherType = StructuredDataPublisherTypes.Person,
            PublisherName = "Ana Lopez",
            PublisherLogoUrl = "https://example.test/logo.png",
            SameAs = ["https://social.example.test/ana"],
            IncludeSearchAction = false,
        };

        // Act
        var imported = await RoundTripAsync(new global::CrestApps.OrchardCore.Seo.DeploymentStartup(), settings);

        // Assert
        Assert.False(imported.RenderOnPages);
        Assert.Equal(StructuredDataPublisherTypes.Person, imported.PublisherType);
        Assert.Equal("Ana Lopez", imported.PublisherName);
        Assert.Equal("https://example.test/logo.png", imported.PublisherLogoUrl);
        Assert.Equal(["https://social.example.test/ana"], imported.SameAs);
        Assert.False(imported.IncludeSearchAction);
        Assert.True(imported.IncludeWebSite);
    }

    [Fact]
    public async Task NLWebSettings_RoundTrip()
    {
        // Arrange
        var settings = new NLWebSettings
        {
            IndexProfileNames = ["Articles"],
            ContentTypes = ["Article", "BlogPost"],
            MaxResults = 25,
            DefaultMode = NLWebProtocol.Modes.Summarize,
            RankResults = true,
            AllowGenerate = false,
            DeploymentName = "gpt-4o",
            DataSourceId = "data-source-1",
        };

        // Act
        var imported = await RoundTripAsync(new global::CrestApps.OrchardCore.NLWeb.DeploymentStartup(), settings);

        // Assert
        Assert.Equal(["Articles"], imported.IndexProfileNames);
        Assert.Equal(["Article", "BlogPost"], imported.ContentTypes);
        Assert.Equal(25, imported.MaxResults);
        Assert.Equal(NLWebProtocol.Modes.Summarize, imported.DefaultMode);
        Assert.True(imported.RankResults);
        Assert.False(imported.AllowGenerate);
        Assert.Equal("gpt-4o", imported.DeploymentName);
        Assert.Equal("data-source-1", imported.DataSourceId);
    }

    [Fact]
    public async Task AIMemorySettings_RoundTrip()
    {
        // Arrange
        var settings = new AIMemorySettings
        {
            IndexProfileName = "Memories",
            TopN = 8,
        };

        // Act
        var imported = await RoundTripAsync(new global::CrestApps.OrchardCore.AI.Memory.DeploymentStartup(), settings);

        // Assert
        Assert.Equal("Memories", imported.IndexProfileName);
        Assert.Equal(8, imported.TopN);
    }

    private static async Task<T> RoundTripAsync<T>(StartupBase startup, T settings)
        where T : class, new()
    {
        var source = new SiteSettings();

        source.Put(settings);

        var siteService = new Mock<ISiteService>();

        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(source);

        var services = new ServiceCollection();

        services.AddSingleton(siteService.Object);
        startup.ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();

        // The module registers the deployment source that writes its settings.
        var deploymentSource = Assert.Single(provider.GetServices<IDeploymentSource>().OfType<SiteSettingsPropertyDeploymentSource<T>>());
        var exported = await RecipeRoundTrip.ExportAsync(deploymentSource, new SiteSettingsPropertyDeploymentStep<T>());

        Assert.Equal("Settings", exported["name"].GetValue<string>());

        // Orchard Core's Settings step stores each member it doesn't know as a site property of the same name.
        var target = new SiteSettings();

        target.Properties[typeof(T).Name] = Assert.IsType<JsonObject>(exported[typeof(T).Name]).DeepClone();

        return target.As<T>();
    }
}
