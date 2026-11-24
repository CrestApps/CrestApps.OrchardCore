using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// The new calendar AI tools are registered by the feature that owns their page, with a title, a description and the
/// Calendar category, and ask for approval where they share, delete, email attendees or reset a feed address.
/// The six tools that existed before are unchanged.
/// </summary>
public sealed class CalendarAIToolRegistrationTests
{
    internal static readonly (string Name, bool RequiresApproval, Type Startup)[] NewTools =
    [
        (GetCalendarTool.ToolName, false, typeof(CalendarManagementAIToolsStartup)),
        (SaveCalendarTool.ToolName, false, typeof(CalendarManagementAIToolsStartup)),
        (ShareCalendarTool.ToolName, true, typeof(CalendarManagementAIToolsStartup)),
        (DeleteCalendarTool.ToolName, true, typeof(CalendarManagementAIToolsStartup)),
        (GetCalendarEventTool.ToolName, false, typeof(CalendarManagementAIToolsStartup)),
        (UpdateCalendarEventTool.ToolName, true, typeof(CalendarManagementAIToolsStartup)),
        (DeleteCalendarEventTool.ToolName, true, typeof(CalendarManagementAIToolsStartup)),
        (RespondToCalendarEventTool.ToolName, true, typeof(CalendarManagementAIToolsStartup)),
        (ListCalendarCategoriesTool.ToolName, false, typeof(CalendarManagementAIToolsStartup)),
        (SaveCalendarCategoryTool.ToolName, false, typeof(CalendarManagementAIToolsStartup)),
        (DeleteCalendarCategoryTool.ToolName, true, typeof(CalendarManagementAIToolsStartup)),
        (GetTeamAvailabilityTool.ToolName, false, typeof(TeamCalendarsAIToolsStartup)),
        (ResetCalendarFeedAddressTool.ToolName, true, typeof(CalendarFeedsAIToolsStartup)),
    ];

    private static readonly Dictionary<Type, string> _features = new()
    {
        [typeof(CalendarManagementAIToolsStartup)] = CalendarsConstants.Features.Calendars,
        [typeof(TeamCalendarsAIToolsStartup)] = CalendarsConstants.Features.Teams,
        [typeof(CalendarFeedsAIToolsStartup)] = CalendarsConstants.Features.Feeds,
    };

    public static TheoryData<string, bool> Tools
    {
        get
        {
            var data = new TheoryData<string, bool>();

            foreach (var (name, requiresApproval, _) in NewTools)
            {
                data.Add(name, requiresApproval);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName, bool requiresApproval)
    {
        // Arrange
        var startup = NewTools.Single(tool => tool.Name == toolName).Startup;
        var tools = GetTools(startup);

        // Act
        var found = tools.TryGetValue(toolName, out var tool);

        // Assert
        Assert.True(found, $"The tool '{toolName}' is not registered by {startup.Name}.");
        Assert.Equal(toolName, tool.Name);
        Assert.Equal(toolName, Create(toolName).Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.NotEqual(toolName, tool.Title);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Calendar", tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
    }

    [Fact]
    public void EachStartup_RegistersOnlyItsToolsAndBelongsToItsFeature()
    {
        foreach (var (startup, featureId) in _features)
        {
            // Act
            var tools = GetTools(startup);
            var required = startup.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false)
                .Cast<RequireFeaturesAttribute>()
                .Single();
            var feature = startup.GetCustomAttributes(typeof(FeatureAttribute), inherit: false)
                .Cast<FeatureAttribute>()
                .Single();

            // Assert
            Assert.Equal(NewTools.Count(tool => tool.Startup == startup), tools.Count);
            Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
            Assert.Equal(featureId, feature.FeatureName);
        }
    }

    [Fact]
    public void NewStartups_RegisterThirteenToolsWithSevenApprovals_AndTheSixExistingToolsAreUnchanged()
    {
        // Act
        var tools = _features.Keys.SelectMany(startup => GetTools(startup).Values).ToList();
        var existing = GetTools(typeof(AIToolsStartup));

        // Assert
        Assert.Equal(13, tools.Count);
        Assert.Equal(13, tools.Select(tool => tool.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(7, tools.Count(tool => tool.RequiresApproval));
        Assert.Equal(
            [CancelCalendarEventTool.ToolName, CreateCalendarEventTool.ToolName, FindCalendarEventsTool.ToolName, FindFreeTimeTool.ToolName, ListCalendarsTool.ToolName, MoveCalendarEventTool.ToolName],
            existing.Keys.Order(StringComparer.Ordinal));
        Assert.All(existing.Values, tool => Assert.False(tool.RequiresApproval));
        Assert.Empty(tools.Select(tool => tool.Name).Intersect(existing.Keys));
    }

    internal static AIFunction Create(string toolName)
        => toolName switch
        {
            GetCalendarTool.ToolName => new GetCalendarTool(),
            SaveCalendarTool.ToolName => new SaveCalendarTool(),
            ShareCalendarTool.ToolName => new ShareCalendarTool(),
            DeleteCalendarTool.ToolName => new DeleteCalendarTool(),
            GetCalendarEventTool.ToolName => new GetCalendarEventTool(),
            UpdateCalendarEventTool.ToolName => new UpdateCalendarEventTool(),
            DeleteCalendarEventTool.ToolName => new DeleteCalendarEventTool(),
            RespondToCalendarEventTool.ToolName => new RespondToCalendarEventTool(),
            ListCalendarCategoriesTool.ToolName => new ListCalendarCategoriesTool(),
            SaveCalendarCategoryTool.ToolName => new SaveCalendarCategoryTool(),
            DeleteCalendarCategoryTool.ToolName => new DeleteCalendarCategoryTool(),
            GetTeamAvailabilityTool.ToolName => new GetTeamAvailabilityTool(),
            ResetCalendarFeedAddressTool.ToolName => new ResetCalendarFeedAddressTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "Unknown tool."),
        };

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools(Type startupType)
    {
        var services = new ServiceCollection();
        var localizer = Activator.CreateInstance(typeof(PassThroughLocalizer<>).MakeGenericType(startupType));
        var startup = (StartupBase)Activator.CreateInstance(startupType, localizer);

        startup.ConfigureServices(services);

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
