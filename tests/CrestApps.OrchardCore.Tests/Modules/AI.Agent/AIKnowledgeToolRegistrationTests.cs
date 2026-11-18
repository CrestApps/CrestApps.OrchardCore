using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Agent;
using CrestApps.OrchardCore.AI.Agent.DataSources;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.AI;
using CrestApps.OrchardCore.AI.Documents.AI;
using CrestApps.OrchardCore.AI.FileSources.AI;
using CrestApps.OrchardCore.AI.WebCrawlers.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using DataSourcesToolsStartup = CrestApps.OrchardCore.AI.DataSources.AIToolsStartup;
using DocumentsToolsStartup = CrestApps.OrchardCore.AI.Documents.AIToolsStartup;
using FileSourcesToolsStartup = CrestApps.OrchardCore.AI.FileSources.AIToolsStartup;
using FtpMetadata = CrestApps.Core.AI.Ftp.Models.FtpConnectionMetadata;
using SftpMetadata = CrestApps.Core.AI.Sftp.Models.SftpConnectionMetadata;
using WebCrawlersToolsStartup = CrestApps.OrchardCore.AI.WebCrawlers.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Agent;

/// <summary>
/// The AI data source, document, file source and web crawler tools are registered with a title, a description and a
/// category, can be picked on a profile, ask for approval where they re-embed content or change what a knowledge base
/// holds, and are registered by the startup of the feature they belong to.
/// </summary>
public sealed class AIKnowledgeToolRegistrationTests
{
    private static readonly Type[] _startups =
    [
        typeof(AIDataSourcesToolsStartup),
        typeof(DataSourcesToolsStartup),
        typeof(DocumentsToolsStartup),
        typeof(FileSourcesToolsStartup),
        typeof(WebCrawlersToolsStartup),
    ];

    public static TheoryData<string, bool> Tools => new()
    {
        { ListAIDataSourcesTool.TheName, false },
        { SaveAIDataSourceTool.TheName, true },
        { DeleteAIDataSourceTool.TheName, true },
        { SyncAIDataSourceTool.TheName, true },
        { SearchAIDataSourceTool.TheName, false },
        { ListAIProfileDocumentsTool.TheName, false },
        { ListAIFileSourcesTool.TheName, false },
        { SaveAIFileSourceTool.TheName, true },
        { DeleteAIFileSourceTool.TheName, true },
        { RunAIFileSourceTool.TheName, true },
        { ListAIWebCrawlersTool.TheName, false },

        // The catalog has no approval here, but saving a crawler queues a full synchronization of its data source,
        // which crawls the site and generates embeddings.
        { SaveAIWebCrawlerTool.TheName, true },
        { DeleteAIWebCrawlerTool.TheName, true },
        { RunAIWebCrawlerTool.TheName, true },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName, bool requiresApproval)
    {
        // Arrange
        var tools = GetTools(_startups);

        // Act
        var found = tools.TryGetValue(toolName, out var tool);

        // Assert
        Assert.True(found, $"The tool '{toolName}' is not registered.");
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.NotEqual(toolName, tool.Title);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.False(string.IsNullOrWhiteSpace(tool.Category));
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
    }

    [Theory]
    [InlineData(typeof(AIDataSourcesToolsStartup), 2, AIConstants.Feature.DataSources)]
    [InlineData(typeof(DataSourcesToolsStartup), 3, AIConstants.Feature.Area)]
    [InlineData(typeof(DocumentsToolsStartup), 1, AIConstants.Feature.Area)]
    [InlineData(typeof(FileSourcesToolsStartup), 4, AIConstants.Feature.Area)]
    [InlineData(typeof(WebCrawlersToolsStartup), 4, AIConstants.Feature.Area)]
    public void EachFeatureStartup_RegistersItsToolsAndRequiresItsFeature(Type startupType, int expectedCount, string featureId)
    {
        // Act
        var tools = GetTools([startupType]);
        var attribute = startupType.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false)
            .Cast<RequireFeaturesAttribute>()
            .Single();

        // Assert
        Assert.Equal(expectedCount, tools.Count);
        Assert.Contains(featureId, attribute.RequiredFeatureNames);
    }

    [Fact]
    public void TheDocumentsToolStartup_BelongsToTheProfileDocumentsFeature()
    {
        // Act
        var feature = typeof(DocumentsToolsStartup).GetCustomAttributes(typeof(FeatureAttribute), inherit: false)
            .Cast<FeatureAttribute>()
            .Single();

        // Assert
        Assert.Equal(AIConstants.Feature.ProfileDocuments, feature.FeatureName);
    }

    [Fact]
    public void TheFtpAndSftpNamesReadByName_MatchTheirPackages()
    {
        // The File Sources module does not reference the FTP and SFTP packages, so it reads their settings by name.
        Assert.Equal(CrestApps.Core.AI.Ftp.FtpIngestionConnector.ConnectorName, FileSourceToolSupport.FtpConnectorName);
        Assert.Equal(CrestApps.Core.AI.Sftp.SftpIngestionConnector.ConnectorName, FileSourceToolSupport.SftpConnectorName);
        Assert.Equal(typeof(FtpMetadata).Name, FileSourceToolSupport.FtpConnectionKey);
        Assert.Equal(typeof(SftpMetadata).Name, FileSourceToolSupport.SftpConnectionKey);

        foreach (var (_, member) in FileSourceToolSupport.FtpFields)
        {
            Assert.NotNull(typeof(FtpMetadata).GetProperty(member));
        }

        foreach (var member in FileSourceToolSupport.FtpSecretMembers)
        {
            Assert.NotNull(typeof(FtpMetadata).GetProperty(member));
        }

        foreach (var (_, member) in FileSourceToolSupport.SftpFields)
        {
            Assert.NotNull(typeof(SftpMetadata).GetProperty(member));
        }

        foreach (var member in FileSourceToolSupport.SftpSecretMembers.Append(FileSourceToolSupport.SftpProxySecretMember))
        {
            Assert.NotNull(typeof(SftpMetadata).GetProperty(member));
        }
    }

    [Fact]
    public void EveryStoredFtpAndSftpSecret_IsReportedOnlyAsAFlag()
    {
        // Each text member whose name marks a secret must be one the tools report as a flag and never write.
        var reported = FileSourceToolSupport.FtpSecretMembers
            .Concat(FileSourceToolSupport.SftpSecretMembers)
            .Append(FileSourceToolSupport.SftpProxySecretMember)
            .ToHashSet(StringComparer.Ordinal);

        var secrets = typeof(FtpMetadata).GetProperties()
            .Concat(typeof(SftpMetadata).GetProperties())
            .Where(property => property.PropertyType == typeof(string) && CrestApps.OrchardCore.AI.Core.Recipes.IngestionSourceSecrets.IsSecret(property.Name))
            .Select(property => property.Name);

        Assert.All(secrets, secret => Assert.Contains(secret, reported));
        Assert.DoesNotContain(FileSourceToolSupport.FtpFields, field => reported.Contains(field.Member));
        Assert.DoesNotContain(FileSourceToolSupport.SftpFields, field => reported.Contains(field.Member));
    }

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools(IEnumerable<Type> startupTypes)
    {
        var services = new ServiceCollection();

        foreach (var type in startupTypes)
        {
            var constructor = type.GetConstructors().Single();
            var arguments = constructor.GetParameters()
                .Select(parameter => Activator.CreateInstance(typeof(PassThroughLocalizer<>).MakeGenericType(parameter.ParameterType.GetGenericArguments()[0])))
                .ToArray();

            ((StartupBase)constructor.Invoke(arguments)).ConfigureServices(services);
        }

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
