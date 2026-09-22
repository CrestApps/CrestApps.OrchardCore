using System.Reflection;
using CrestApps.Core.AI.Documents.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Documents;

public sealed class InteractionDocumentOptionsRegistrationTests
{
    [Fact]
    public void ConfigureServices_AppliesSiteDocumentOverrides()
    {
        var settings = new InteractionDocumentSettings
        {
            IndexProfileName = "docs-index",
            TopN = 7,
            RetrievalMode = DocumentRetrievalMode.Hierarchical,
            AllowDocumentUploads = false,
            AllowImageUploads = true,
            MaxIndexableCharacters = 1234,
            DescribeFiguresInUploads = false,
        };

        var services = new ServiceCollection();
        services.AddSingleton(CreateSiteService(settings));

        new CrestApps.OrchardCore.AI.Documents.Startup().ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<InteractionDocumentOptions>>().Value;

        Assert.Equal("docs-index", options.IndexProfileName);
        Assert.Equal(7, options.TopN);
        Assert.Equal(DocumentRetrievalMode.Hierarchical, options.RetrievalMode);
        Assert.False(options.AllowDocumentUploads);
        Assert.True(options.AllowImageUploads);
        Assert.Equal(1234, options.MaxIndexableCharacters);
        Assert.False(options.DescribeFiguresInUploads);
    }

    /// <summary>
    /// Checks that every stored interaction-document setting reaches the options type the processing
    /// service actually reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="InteractionDocumentSettings"/> is what the settings screen persists;
    /// <see cref="InteractionDocumentOptions"/> is what uploads are measured against, and
    /// <c>InteractionDocumentOptionsConfiguration</c> copies one onto the other property by property. A
    /// setting missing from that copy is silently dead: the screen saves it, reloads it and shows it back,
    /// while uploads keep obeying the compiled-in default.
    /// </para>
    /// <para>
    /// This reflects over both types rather than listing the properties, so a setting added later cannot
    /// arrive dead without failing here first.
    /// </para>
    /// </remarks>
    [Fact]
    public void EverySetting_IsCarriedOntoTheOptionsType()
    {
        var settings = new InteractionDocumentSettings
        {
            IndexProfileName = "parity-index",
            TopN = 9,
            RetrievalMode = DocumentRetrievalMode.Hierarchical,
            AllowDocumentUploads = false,
            AllowImageUploads = true,
            MaxIndexableCharacters = 4321,
            DescribeFiguresInUploads = false,
        };

        var services = new ServiceCollection();
        services.AddSingleton(CreateSiteService(settings));

        new CrestApps.OrchardCore.AI.Documents.Startup().ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<InteractionDocumentOptions>>().Value;

        var optionProperties = typeof(InteractionDocumentOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(property => property.Name, StringComparer.Ordinal);

        var problems = new List<string>();

        foreach (var setting in typeof(InteractionDocumentSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!optionProperties.TryGetValue(setting.Name, out var option) || option.PropertyType != setting.PropertyType)
            {
                problems.Add($"{setting.PropertyType.Name} {setting.Name} has no matching option.");
                continue;
            }

            var stored = setting.GetValue(settings);

            if (!Equals(stored, option.GetValue(options)))
            {
                problems.Add($"{setting.Name} is not copied onto the options type.");
            }
        }

        Assert.True(
            problems.Count == 0,
            $"{nameof(InteractionDocumentOptions)} does not carry every stored setting: {string.Join(" ", problems)} " +
            "A setting that never reaches the options type is never read, however well the settings screen saves it.");
    }

    /// <summary>
    /// Checks that an unconfigured host enforces the same defaults the settings screen presents.
    /// </summary>
    [Fact]
    public void TheTwoUploadCeilings_CarryTheSameDefaults()
    {
        var settings = new InteractionDocumentSettings();
        var options = new InteractionDocumentOptions();

        Assert.Equal(settings.MaxIndexableCharacters, options.MaxIndexableCharacters);
        Assert.Equal(settings.DescribeFiguresInUploads, options.DescribeFiguresInUploads);
    }

    private static ISiteService CreateSiteService(InteractionDocumentSettings settings)
    {
        var site = new Mock<ISite>();
        site.Setup(x => x.GetOrCreate<InteractionDocumentSettings>())
            .Returns(settings);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(x => x.GetSiteSettingsAsync())
            .ReturnsAsync(site.Object);

        return siteService.Object;
    }
}
