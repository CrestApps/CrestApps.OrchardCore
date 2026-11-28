using System.Globalization;
using System.Text.Json;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Scheduling;
using CrestApps.OrchardCore.Scheduling.AI;
using CrestApps.OrchardCore.Scheduling.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Scheduling;

/// <summary>
/// The scheduling administration AI tools are registered by their own startup on the Scheduling feature, with a title, a
/// description and the Scheduling category, and ask for approval where they delete or can message the customer. The
/// scheduling resources are the <c>schedulingResource</c> record kind, with the Manage scheduling permission. The four
/// customer-facing tools that existed before are unchanged.
/// </summary>
public sealed class SchedulingAdminAIToolRegistrationTests
{
    internal static readonly (string Name, bool RequiresApproval, string Permission)[] NewTools =
    [
        (ListAppointmentsTool.ToolName, false, SchedulingPermissions.ViewAppointments.Name),
        (GetAppointmentTool.ToolName, false, SchedulingPermissions.ViewAppointments.Name),
        (SetAppointmentStatusTool.ToolName, true, SchedulingPermissions.ManageAppointments.Name),
        (GetAppointmentTypeTool.ToolName, false, SchedulingPermissions.ManageScheduling.Name),
        (SaveAppointmentTypeTool.ToolName, false, SchedulingPermissions.ManageScheduling.Name),
        (DeleteAppointmentTypeTool.ToolName, true, SchedulingPermissions.ManageScheduling.Name),
        (ListAppointmentWaitlistTool.ToolName, false, SchedulingPermissions.ManageAppointments.Name),
        (AddToAppointmentWaitlistTool.ToolName, false, SchedulingPermissions.ManageAppointments.Name),
        (RemoveFromAppointmentWaitlistTool.ToolName, false, SchedulingPermissions.ManageAppointments.Name),
    ];

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
        var tools = GetTools(typeof(SchedulingAdminAIToolsStartup));

        // Act
        var found = tools.TryGetValue(toolName, out var tool);

        // Assert
        Assert.True(found, $"The tool '{toolName}' is not registered.");
        Assert.Equal(toolName, tool.Name);
        Assert.Equal(toolName, Create(toolName).Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.NotEqual(toolName, tool.Title);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.False(string.IsNullOrWhiteSpace(Create(toolName).Description));
        Assert.Equal("Scheduling", tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
    }

    [Fact]
    public void NewStartup_RegistersNineToolsWithTwoApprovals_OnTheSchedulingFeature_AndTheFourExistingToolsAreUnchanged()
    {
        // Act
        var tools = GetTools(typeof(SchedulingAdminAIToolsStartup));
        var existing = GetTools(typeof(AIToolsStartup));
        var required = typeof(SchedulingAdminAIToolsStartup).GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();
        var feature = typeof(SchedulingAdminAIToolsStartup).GetCustomAttributes(typeof(FeatureAttribute), inherit: false).Cast<FeatureAttribute>().Single();

        // Assert
        Assert.Equal(9, tools.Count);
        Assert.Equal(2, tools.Values.Count(tool => tool.RequiresApproval));
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
        Assert.Equal(SchedulingConstants.Features.Scheduling, feature.FeatureName);
        Assert.Equal(
            new[] { BookAppointmentTool.ToolName, ChangeAppointmentTool.ToolName, FindAppointmentSlotsTool.ToolName, ListAppointmentTypesTool.ToolName },
            existing.Keys.Order(StringComparer.Ordinal));
        Assert.All(existing.Values, tool => Assert.False(tool.RequiresApproval));
        Assert.Empty(tools.Keys.Intersect(existing.Keys));
        Assert.DoesNotContain("listSchedulingResources", tools.Keys);
        Assert.DoesNotContain("saveSchedulingResource", tools.Keys);
        Assert.DoesNotContain("deleteSchedulingResource", tools.Keys);
    }

    [Fact]
    public void NewStartup_RegistersTheSchedulingResourceKind_WithManageScheduling_AndNoAppointmentTypeKind()
    {
        // Act
        var kinds = GetKinds(typeof(SchedulingAdminAIToolsStartup));

        // Assert
        var kind = Assert.Single(kinds.Values);

        Assert.Equal(SchedulingResourceRecordKind.Name, kind.Name);
        Assert.Equal(typeof(SchedulingResource), kind.RecordType);
        Assert.True(kind.CanWrite);
        Assert.Equal(SchedulingPermissions.ManageScheduling.Name, kind.Permission.Name);
        Assert.Equal(SchedulingPermissions.ManageScheduling.Name, kind.EffectiveReadPermission.Name);
        Assert.False(string.IsNullOrWhiteSpace(kind.Description));
        Assert.NotEqual(kind.Name, kind.Title);
    }

    [Fact]
    public void SchedulingResourceKind_ReturnsTheEditorsTextForms_AndNeverTheRecordsProperties()
    {
        // Arrange
        var resource = new SchedulingResource
        {
            ItemId = "lee",
            Name = "Dr. Lee",
            ShiftCalendarId = "old-shifts",
            ShiftCalendarIds = ["shifts"],
            WorkingHours = [new SchedulingWorkingHours { Day = DayOfWeek.Monday, Start = "09:00", End = "12:00" }, new SchedulingWorkingHours { Day = DayOfWeek.Monday, Start = "13:00", End = "17:00" }],
            DateOverrides = [new SchedulingDateOverride { Date = new DateTime(2026, 12, 24), Closed = true }],
        };

        resource.Properties["Secret"] = "canary";

        // Act
        var json = JsonSerializer.SerializeToElement(SchedulingResourceRecordKind.Project(resource));

        // Assert
        Assert.Equal("Monday 09:00-12:00, 13:00-17:00", json.GetProperty("workingHours").GetString());
        Assert.Equal("2026-12-24 closed", json.GetProperty("dateOverrides").GetString());
        Assert.Equal(new[] { "shifts", "old-shifts" }, json.GetProperty("shiftCalendarIds").EnumerateArray().Select(id => id.GetString()));
        Assert.DoesNotContain("canary", json.GetRawText(), StringComparison.Ordinal);
        Assert.False(json.TryGetProperty("properties", out _));
    }

    internal static AIFunction Create(string toolName)
        => toolName switch
        {
            ListAppointmentsTool.ToolName => new ListAppointmentsTool(),
            GetAppointmentTool.ToolName => new GetAppointmentTool(),
            SetAppointmentStatusTool.ToolName => new SetAppointmentStatusTool(),
            GetAppointmentTypeTool.ToolName => new GetAppointmentTypeTool(),
            SaveAppointmentTypeTool.ToolName => new SaveAppointmentTypeTool(),
            DeleteAppointmentTypeTool.ToolName => new DeleteAppointmentTypeTool(),
            ListAppointmentWaitlistTool.ToolName => new ListAppointmentWaitlistTool(),
            AddToAppointmentWaitlistTool.ToolName => new AddToAppointmentWaitlistTool(),
            RemoveFromAppointmentWaitlistTool.ToolName => new RemoveFromAppointmentWaitlistTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "Unknown tool."),
        };

    private static IReadOnlyDictionary<string, AIRecordKind> GetKinds(Type startupType)
    {
        var services = new ServiceCollection();
        var localizer = Activator.CreateInstance(typeof(PassThroughLocalizer<>).MakeGenericType(startupType));
        var startup = (StartupBase)Activator.CreateInstance(startupType, localizer);

        startup.ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds;
    }

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
