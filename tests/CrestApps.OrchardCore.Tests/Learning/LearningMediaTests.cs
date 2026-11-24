using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// Remaining work section 3, item 11: the original file name shown to learners instead of the stored name, and Orchard
/// Core Media's limits left to Media's own configuration (review round 11, R3).
/// </summary>
public sealed class LearningMediaTests
{
    [Fact]
    public void Round11R3_EnablingLearning_LeavesMediaOptionsAsConfigured()
    {
        // Arrange: Orchard Core Media configured by the site, then Learning's services added.
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<MediaOptions>(options =>
        {
            options.MaxFileSize = 30_000_000;
            options.AllowedFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf" };
        });

        new CrestApps.OrchardCore.Learning.Startup(new TestShellConfiguration(new ConfigurationBuilder().Build())).ConfigureServices(services);

        // Act
        var media = services.BuildServiceProvider().GetRequiredService<IOptions<MediaOptions>>().Value;

        // Assert
        Assert.Equal(30_000_000, media.MaxFileSize);
        Assert.Equal([".pdf"], media.AllowedFileExtensions);
    }

    [Theory]
    [InlineData("mediafields/FileLesson/4abc/3f2504e0-4f89-11d3-9a0c-0305e82c3301_Safety plan.ods", null, null, "Safety plan.ods")]
    [InlineData("mediafields/FileLesson/4abc/3f2504e04f8911d39a0c0305e82c3301_Safety plan.ods", null, null, "Safety plan.ods")]
    [InlineData("mediafields/FileLesson/4abc/3F2504E04F8911D39A0C0305E82C3301-report.pdf", null, null, "report.pdf")]
    [InlineData("mediafields/FileLesson/4abc/1a2b3c4d.pdf", "Handbook 2026.pdf", null, "Handbook 2026.pdf")]
    [InlineData("mediafields/FileLesson/4abc/1a2b3c4d.pdf", "Handbook 2026.pdf", "The handbook", "The handbook")]
    [InlineData("courses/handbook.pdf", null, null, "handbook.pdf")]
    [InlineData("courses/handbook.pdf", "  ", " ", "handbook.pdf")]
    public void DisplayName_IsTheOriginalName(string path, string attachedName, string mediaText, string expected)
    {
        // Act
        var name = LearningFileNames.GetDisplayName(path, attachedName, mediaText);

        // Assert
        Assert.Equal(expected, name);
    }

    private sealed class TestShellConfiguration : IShellConfiguration
    {
        private readonly IConfiguration _configuration;

        public TestShellConfiguration(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string this[string key]
        {
            get => _configuration[key];
            set => _configuration[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => _configuration.GetChildren();

        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => _configuration.GetReloadToken();

        public IConfigurationSection GetSection(string key) => _configuration.GetSection(key);
    }
}
