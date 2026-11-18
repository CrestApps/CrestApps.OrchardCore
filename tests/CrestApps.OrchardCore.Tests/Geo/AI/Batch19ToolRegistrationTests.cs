using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Accounts.AI;
using CrestApps.OrchardCore.Addresses.AI;
using CrestApps.OrchardCore.Geo.AI;
using CrestApps.OrchardCore.PhoneNumbers.AI;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.AI;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using CrestApps.OrchardCore.TimeZones.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using AccountsAIToolsStartup = CrestApps.OrchardCore.Accounts.AIToolsStartup;
using AddressesAIToolsStartup = CrestApps.OrchardCore.Addresses.AIToolsStartup;
using GeoAIToolsStartup = CrestApps.OrchardCore.Geo.AIToolsStartup;
using PhoneNumbersAIToolsStartup = CrestApps.OrchardCore.PhoneNumbers.AIToolsStartup;
using TimeZonesAIToolsStartup = CrestApps.OrchardCore.TimeZones.AIToolsStartup;
using VerificationsAIToolsStartup = CrestApps.OrchardCore.PhoneNumbers.Verifications.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.Geo.AI;

/// <summary>
/// The tools of batch 19 (time zones, addresses, phone numbers, phone verifications, geo, accounts) are registered with a
/// title, a description and a category, can be picked on a profile, ask for approval as the catalog says (deletes,
/// billable lookups and account members), and are registered by the startup of the feature they belong to, which
/// requires the AI feature.
/// </summary>
public sealed class Batch19ToolRegistrationTests
{
    private static readonly Type[] _startups =
    [
        typeof(TimeZonesAIToolsStartup),
        typeof(AddressesAIToolsStartup),
        typeof(PhoneNumbersAIToolsStartup),
        typeof(VerificationsAIToolsStartup),
        typeof(GeoAIToolsStartup),
        typeof(AccountsAIToolsStartup),
        typeof(MembersAIToolsStartup),
        typeof(LocationsAIToolsStartup),
        typeof(TeamAIToolsStartup),
    ];

    public static TheoryData<string, bool> Tools => new()
    {
        { ListTimeZoneMapsTool.TheName, false },
        { SaveTimeZoneMapTool.TheName, false },
        { DeleteTimeZoneMapTool.TheName, true },
        { ValidateAddressTool.TheName, false },
        { NormalizePhoneNumberTool.TheName, false },
        { ListPhoneNumberVerificationsTool.TheName, false },
        { RetryPhoneNumberVerificationsTool.TheName, true },
        { ListServiceTerritoriesTool.TheName, false },
        { SaveServiceTerritoryTool.TheName, false },
        { DeleteServiceTerritoryTool.TheName, true },
        { FindServiceTerritoryTool.TheName, false },
        { GeocodeAddressTool.TheName, true },
        { GetGeocodingStatusTool.TheName, false },
        { ListAccountsTool.TheName, false },
        { SetAccountMembersTool.TheName, true },
        { SaveAccountLocationTool.TheName, false },
        { DeleteAccountLocationTool.TheName, true },
        { SetAccountTeamTool.TheName, false },
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

    [Fact]
    public void TheBatch_RegistersEighteenTools()
    {
        // Act
        var tools = GetTools(_startups);

        // Assert
        Assert.Equal(18, tools.Count);
    }

    [Theory]
    [InlineData(typeof(TimeZonesAIToolsStartup), 3, null)]
    [InlineData(typeof(AddressesAIToolsStartup), 1, null)]
    [InlineData(typeof(PhoneNumbersAIToolsStartup), 1, null)]
    [InlineData(typeof(VerificationsAIToolsStartup), 2, null)]
    [InlineData(typeof(GeoAIToolsStartup), 6, null)]
    [InlineData(typeof(AccountsAIToolsStartup), 1, null)]
    [InlineData(typeof(MembersAIToolsStartup), 1, AccountsConstants.MembersFeature)]
    [InlineData(typeof(LocationsAIToolsStartup), 2, AccountsConstants.LocationsFeature)]
    [InlineData(typeof(TeamAIToolsStartup), 1, AccountsConstants.TeamFeature)]
    public void EachStartup_RegistersItsTools_ForItsFeature_WhenTheAIFeatureIsOn(Type startupType, int expectedCount, string featureId)
    {
        // Act
        var tools = GetTools([startupType]);
        var required = startupType.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false)
            .Cast<RequireFeaturesAttribute>()
            .Single();
        var feature = startupType.GetCustomAttributes(typeof(FeatureAttribute), inherit: false)
            .Cast<FeatureAttribute>()
            .SingleOrDefault();

        // Assert
        Assert.Equal(expectedCount, tools.Count);
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
        Assert.Equal(featureId, feature?.FeatureName);
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
