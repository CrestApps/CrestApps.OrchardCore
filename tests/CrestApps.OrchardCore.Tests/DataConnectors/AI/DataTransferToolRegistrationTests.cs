using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.AI;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.AI;
using CrestApps.OrchardCore.DataConnectors.SqlServer;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using ContentTransferToolsStartup = CrestApps.OrchardCore.ContentTransfer.AIToolsStartup;
using DataConnectorsToolsStartup = CrestApps.OrchardCore.DataConnectors.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.DataConnectors.AI;

/// <summary>
/// The data connector and content transfer tools are registered with a title, a description and a category, can be
/// picked on a profile, ask for approval as the catalog says, and are registered by the startup of the feature they
/// belong to, which requires the AI feature.
/// </summary>
public sealed class DataTransferToolRegistrationTests
{
    public static TheoryData<string, bool> Tools => new()
    {
        { ListDataConnectionsTool.TheName, false },
        { SaveDataConnectionTool.TheName, true },
        { TestDataConnectionTool.TheName, false },
        { DeleteDataConnectionTool.TheName, true },
        { ListDataConnectionResourcesTool.TheName, false },
        { ListConnectedDataSetsTool.TheName, false },
        { GetConnectedDataSetTool.TheName, false },
        { SaveConnectedDataSetTool.TheName, false },
        { RefreshConnectedDataSetTool.TheName, false },
        { PreviewConnectedDataSetTool.TheName, false },
        { DeleteConnectedDataSetTool.TheName, true },
        { ListContentTransferTypesTool.TheName, false },
        { ListContentTransferEntriesTool.TheName, false },
        { GetContentTransferEntryTool.TheName, false },
        { SetContentImportStateTool.TheName, false },
        { DeleteContentTransferEntryTool.TheName, true },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName, bool requiresApproval)
    {
        // Arrange
        var tools = GetTools([typeof(DataConnectorsToolsStartup), typeof(ContentTransferToolsStartup)]);

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
    [InlineData(typeof(DataConnectorsToolsStartup), 11, DataConnectorConstants.Features.Area)]
    [InlineData(typeof(ContentTransferToolsStartup), 5, ContentTransferConstants.Feature.ModuleId)]
    public void EachStartup_RegistersItsTools_ForItsFeature_WhenTheAIFeatureIsOn(Type startupType, int expectedCount, string featureId)
    {
        // Act
        var tools = GetTools([startupType]);
        var required = startupType.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false)
            .Cast<RequireFeaturesAttribute>()
            .Single();
        var feature = startupType.GetCustomAttributes(typeof(FeatureAttribute), inherit: false)
            .Cast<FeatureAttribute>()
            .Single();

        // Assert
        Assert.Equal(expectedCount, tools.Count);
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
        Assert.Equal(featureId, feature.FeatureName);
    }

    [Fact]
    public void TheAIFeatureId_MatchesTheAIModule()
        => Assert.Equal(CrestApps.OrchardCore.AI.Core.AIConstants.Feature.Area, AIFeatureIds.Area);

    [Fact]
    public void TheSqlServerNamesReadByName_MatchTheSqlServerModule()
    {
        // The Data Connectors module can't use the SQL Server module's types, so it reads the settings by name.
        Assert.Equal(DataConnectorToolDescriptions.SqlServerSettingsKey, typeof(SqlServerConnectionSettings).Name);
        Assert.Equal(DataConnectorToolDescriptions.SqlServerPasswordSecret, SqlServerDataConnector.PasswordSecret);
        Assert.Equal(Enum.GetNames<SqlServerAuthenticationType>(), DataConnectorToolDescriptions.SqlServerAuthentications);
        Assert.All(DataConnectorToolDescriptions.SqlServerFields, field => Assert.NotNull(typeof(SqlServerConnectionSettings).GetProperty(field)));
        Assert.All(DataConnectorToolDescriptions.SqlServerHostIdentityAuthentications, mode => Assert.True(Enum.IsDefined(Enum.Parse<SqlServerAuthenticationType>(mode))));

        // Every text setting is reported, so a secret must never become one: the password is stored apart.
        Assert.DoesNotContain(typeof(SqlServerConnectionSettings).GetProperties(), property => property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools(IEnumerable<Type> startupTypes)
    {
        var services = new ServiceCollection();

        foreach (var type in startupTypes)
        {
            var constructor = type.GetConstructors().Single();
            var arguments = constructor.GetParameters()
                .Select(parameter => Activator.CreateInstance(typeof(PassThroughStringLocalizer<>).MakeGenericType(parameter.ParameterType.GetGenericArguments()[0])))
                .ToArray();

            ((StartupBase)constructor.Invoke(arguments)).ConfigureServices(services);
        }

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;
    }
}
