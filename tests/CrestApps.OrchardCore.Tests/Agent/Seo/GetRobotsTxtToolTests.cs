using System.Text;
using CrestApps.OrchardCore.AI.Agent.Seo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Moq;
using OrchardCore.Modules.FileProviders;
using OrchardCore.Seo;

namespace CrestApps.OrchardCore.Tests.Agent.Seo;

/// <summary>
/// The robots.txt tool returns what the site serves: a physical robots.txt file when there is one, otherwise what the
/// robots providers write, in the order the site writes them.
/// </summary>
public sealed class GetRobotsTxtToolTests
{
    [Fact]
    public async Task InvokeAsync_WithoutAFile_BuildsTheContentFromTheProvidersLastRegisteredFirst()
    {
        // Arrange
        var services = CreateServices(file: null, "User-agent: *\nDisallow: /admin\n", "   ", "Sitemap: https://example.com/sitemap.xml");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetRobotsTxtTool(), services);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("settings", json.GetProperty("source").GetString());
        Assert.Equal(
            $"Sitemap: https://example.com/sitemap.xml{Environment.NewLine}User-agent: *\nDisallow: /admin{Environment.NewLine}",
            json.GetProperty("content").GetString());
        Assert.False(json.TryGetProperty("truncated", out _));
        Assert.Equal("ManageSeoSettings", Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
    }

    [Fact]
    public async Task InvokeAsync_WithAPhysicalFile_ReturnsTheFileAndSaysTheSettingsHaveNoEffect()
    {
        // Arrange
        var services = CreateServices(file: "User-agent: *\nDisallow: /", "User-agent: *\nAllow: /");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetRobotsTxtTool(), services);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("file", json.GetProperty("source").GetString());
        Assert.Equal("User-agent: *\nDisallow: /", json.GetProperty("content").GetString());
        Assert.Contains("have no effect", json.GetProperty("note").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_WithAVeryLongFile_CutsTheContentAndSaysSo()
    {
        // Arrange
        var services = CreateServices(file: new string('a', 25_000));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetRobotsTxtTool(), services);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("truncated").GetBoolean());
        Assert.Equal(20_000, json.GetProperty("content").GetString().Length);
    }

    private static ServiceProvider CreateServices(string file, params string[] providerContents)
    {
        var fileInfo = new Mock<IFileInfo>();
        fileInfo.Setup(info => info.Exists).Returns(file is not null);
        fileInfo.Setup(info => info.CreateReadStream()).Returns(() => new MemoryStream(Encoding.UTF8.GetBytes(file ?? string.Empty)));

        var staticFileProvider = new Mock<IStaticFileProvider>();
        staticFileProvider.Setup(provider => provider.GetFileInfo(SeoConstants.RobotsFileName)).Returns(fileInfo.Object);

        var services = AgentToolTestContext.CreateServices();

        services.AddSingleton(staticFileProvider.Object);

        foreach (var content in providerContents)
        {
            var robotsProvider = new Mock<IRobotsProvider>();
            robotsProvider.Setup(provider => provider.GetContentAsync()).ReturnsAsync(content);

            services.AddSingleton(robotsProvider.Object);
        }

        return services.BuildServiceProvider();
    }
}
