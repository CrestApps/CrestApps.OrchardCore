using System.Text.Json;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.BusinessHours;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.AI;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.Records;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AITools;

/// <summary>
/// The Contact Center setup records are record kinds of the generic record tools, registered by the feature that owns
/// them with the permission its admin page checks. Queue groups, reason codes and dialer profiles are written through
/// their editors; queues, calendars and entry points are read-only, and their fields carry the argument names of the
/// tools that write them. The rules the editors enforce through their option lists hold for the kinds too.
/// </summary>
public sealed class ContactCenterRecordKindTests
{
    private readonly ContactCenterSetupToolHarness _harness = new();

    public ContactCenterRecordKindTests()
    {
        _harness.AddCalendar("window", "Calling window");
        _harness.DialerProfiles.Entries.Add(new DialerProfile
        {
            ItemId = "evening",
            Name = "Evening predictive",
            Mode = DialerMode.Predictive,
            MaxAttempts = 5,
            RespectDoNotCall = true,
            EnforceCallingWindow = true,
            CallingCalendarId = "window",
        });
    }

    public static TheoryData<Type, string, bool, string> Kinds
        => new()
        {
            { typeof(QueuesAIToolsStartup), ContactCenterRecordKinds.QueueGroupKind, true, ContactCenterPermissions.ManageQueueGroups.Name },
            { typeof(QueuesAIToolsStartup), ContactCenterRecordKinds.QueueKind, false, ContactCenterPermissions.ManageQueues.Name },
            { typeof(EntryPointsAIToolsStartup), ContactCenterRecordKinds.EntryPointKind, false, ContactCenterPermissions.ManageQueues.Name },
            { typeof(BusinessHoursAIToolsStartup), ContactCenterRecordKinds.BusinessHoursCalendarKind, false, BusinessHoursPermissions.ManageBusinessHoursSchedules.Name },
            { typeof(AgentsAIToolsStartup), ContactCenterRecordKinds.ReasonCodeKind, true, ContactCenterPermissions.ManageAgents.Name },
            { typeof(DialerAIToolsStartup), ContactCenterRecordKinds.DialerProfileKind, true, ContactCenterPermissions.ManageDialer.Name },
        };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Kind_IsRegisteredByItsFeature_WithThePagesPermission(Type startup, string name, bool canWrite, string permission)
    {
        // Act
        var kind = GetKinds(startup)[name];

        // Assert
        Assert.Equal(canWrite, kind.CanWrite);
        Assert.Equal(permission, kind.Permission.Name);
        Assert.Equal(permission, kind.EffectiveReadPermission.Name);
        Assert.False(string.IsNullOrWhiteSpace(kind.Description));
        Assert.NotEqual(kind.Name, kind.Title);
    }

    public static TheoryData<Type, string, string, string> ReadOnlyKinds
        => new()
        {
            { typeof(QueuesAIToolsStartup), ContactCenterRecordKinds.QueueKind, "saveContactCenterQueue", "deleteContactCenterRecord" },
            { typeof(EntryPointsAIToolsStartup), ContactCenterRecordKinds.EntryPointKind, "saveContactCenterEntryPoint", "deleteContactCenterRecord" },
            { typeof(BusinessHoursAIToolsStartup), ContactCenterRecordKinds.BusinessHoursCalendarKind, "saveBusinessHoursCalendar", "deleteContactCenterRecord" },
        };

    [Theory]
    [MemberData(nameof(ReadOnlyKinds))]
    public void ReadOnlyKind_NamesTheToolsThatSaveAndDeleteIt(Type startup, string name, string saveTool, string deleteTool)
    {
        // Act
        var kind = GetKinds(startup)[name];

        // Assert
        Assert.False(kind.CanWrite);
        Assert.Equal(saveTool, kind.SaveToolName);
        Assert.Equal(deleteTool, kind.DeleteToolName);
        Assert.DoesNotContain(saveTool, kind.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Startups_RegisterSixKinds()
    {
        // Act
        var names = new[] { typeof(QueuesAIToolsStartup), typeof(EntryPointsAIToolsStartup), typeof(BusinessHoursAIToolsStartup), typeof(AgentsAIToolsStartup), typeof(DialerAIToolsStartup) }
            .SelectMany(startup => GetKinds(startup).Keys)
            .Order(StringComparer.Ordinal);

        // Assert
        Assert.Equal(
            new[] { "agentStateReasonCode", "businessHoursCalendar", "contactCenterEntryPoint", "contactCenterQueue", "contactCenterQueueGroup", "dialerProfile" },
            names);
    }

    [Fact]
    public void QueueKind_ReturnsTheArgumentNamesOfSaveContactCenterQueue()
    {
        // Arrange
        var kind = (AIRecordKind<ActivityQueue>)GetKinds(typeof(QueuesAIToolsStartup))[ContactCenterRecordKinds.QueueKind];
        var queue = new ActivityQueue
        {
            ItemId = "sales",
            Name = "Sales",
            QueueGroupId = "group-1",
            OverflowTargets = [new QueueOverflowTarget { QueueId = "billing", AfterSeconds = 60 }],
            SkillRequirements = [new QueueSkillRequirement { SkillId = "spanish", MinimumProficiency = 3, Required = true }],
        };

        // Act
        var projected = JsonSerializer.SerializeToElement(kind.Projection(queue));

        // Assert
        AssertArgumentsOf(new SaveContactCenterQueueTool(), projected);
        Assert.Equal("billing", projected.GetProperty("overflowTargets")[0].GetProperty("queueId").GetString());
        Assert.Equal(3, projected.GetProperty("skillRequirements")[0].GetProperty("minimumProficiency").GetInt32());
    }

    [Fact]
    public void CalendarKind_ReturnsTheArgumentNamesOfSaveBusinessHoursCalendar()
    {
        // Arrange
        var kind = (AIRecordKind<BusinessHoursSchedule>)GetKinds(typeof(BusinessHoursAIToolsStartup))[ContactCenterRecordKinds.BusinessHoursCalendarKind];
        var calendar = _harness.Calendars.Entries.Single();
        calendar.Hours.DateExceptions =
        [
            new HoursDateException { Date = new DateOnly(2026, 12, 24), Name = "Christmas Eve", Closed = false, OpenMinute = 540, CloseMinute = 780 },
            new HoursDateException { Date = new DateOnly(2026, 12, 25), Name = "Christmas Day" },
        ];

        // Act
        var projected = JsonSerializer.SerializeToElement(kind.Projection(calendar));

        // Assert
        AssertArgumentsOf(new SaveBusinessHoursCalendarTool(), projected);
        Assert.Equal("09:00", projected.GetProperty("weeklySchedule")[1].GetProperty("open").GetString());
        Assert.Equal("2026-12-25", Assert.Single(projected.GetProperty("holidays").EnumerateArray()).GetString());
        var specialDays = projected.GetProperty("specialDays");
        Assert.Equal("2026-12-24", specialDays[0].GetProperty("date").GetString());
        Assert.Equal("13:00", specialDays[0].GetProperty("close").GetString());
        Assert.Equal("Christmas Eve", specialDays[0].GetProperty("name").GetString());
        Assert.True(specialDays[1].GetProperty("closed").GetBoolean());
        Assert.Equal("Christmas Day", specialDays[1].GetProperty("name").GetString());
    }

    [Fact]
    public void EntryPointKind_ReturnsTheArgumentNamesOfSaveContactCenterEntryPoint_AndItsPhoneMenu()
    {
        // Arrange
        var kind = (AIRecordKind<ContactCenterEntryPoint>)GetKinds(typeof(EntryPointsAIToolsStartup))[ContactCenterRecordKinds.EntryPointKind];
        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "main",
            Name = "Main line",
            Channel = "Phone",
            AddressIds = ["address-1"],
            TargetQueueId = "sales",
            IvrFlow = new IvrFlow
            {
                RootNodeId = "root",
                Nodes =
                [
                    new IvrNode
                    {
                        NodeId = "root",
                        Prompt = "Press 1 for sales.",
                        Options = [new IvrOption { Digit = "1", Action = new IvrAction { Kind = IvrActionKind.RouteToQueue, TargetId = "sales" } }],
                    },
                ],
            },
            Properties = { ["MessagingEntryPoint"] = "kept" },
        };

        // Act
        var projected = JsonSerializer.SerializeToElement(kind.Projection(entryPoint));

        // Assert
        AssertArgumentsOf(new SaveContactCenterEntryPointTool(), projected, "dialedNumbers", "targetAIProfileId", "otherSettings");

        var option = projected.GetProperty("phoneMenu").GetProperty("nodes")[0].GetProperty("options")[0];
        Assert.Equal("1", option.GetProperty("digit").GetString());
        Assert.Equal("RouteToQueue", option.GetProperty("action").GetProperty("kind").GetString());
        Assert.Equal("MessagingEntryPoint", projected.GetProperty("otherSettings")[0].GetString());
        Assert.False(projected.TryGetProperty("properties", out _));
    }

    [Fact]
    public void ReasonCodeKind_ReturnsOnlyItsAllowListedFields()
    {
        // Arrange
        var kind = (AIRecordKind<AgentStateReasonCode>)GetKinds(typeof(AgentsAIToolsStartup))[ContactCenterRecordKinds.ReasonCodeKind];

        // Act
        var projected = JsonSerializer.SerializeToElement(kind.Projection(new AgentStateReasonCode
        {
            ItemId = "lunch",
            Name = "Lunch",
            AppliesTo = AgentPresenceStatus.Break,
            SortOrder = 2,
            Enabled = true,
            Properties = { ["Secret"] = "never" },
        }));

        // Assert
        Assert.Equal(
            new[] { "appliesTo", "description", "enabled", "id", "name", "sortOrder" },
            projected.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Break", projected.GetProperty("appliesTo").GetString());
    }

    [Fact]
    public async Task ReasonCodeKind_RefusesAStateTheEditorDoesNotOffer()
    {
        // Arrange
        var kind = (AIRecordKind<AgentStateReasonCode>)GetKinds(typeof(AgentsAIToolsStartup))[ContactCenterRecordKinds.ReasonCodeKind];
        var services = _harness.Build();

        // Act
        var refused = await kind.ValidateAsync(services, new AgentStateReasonCode { Name = "Ready", AppliesTo = AgentPresenceStatus.Available }, CancellationToken.None);
        var accepted = await kind.ValidateAsync(services, new AgentStateReasonCode { Name = "Coaching", AppliesTo = AgentPresenceStatus.Away }, CancellationToken.None);

        // Assert
        Assert.False(refused.Succeeded);
        Assert.Contains(refused.Errors, error => error.MemberNames.Contains(nameof(AgentStateReasonCode.AppliesTo)));
        Assert.True(accepted.Succeeded);
    }

    [Fact]
    public async Task DialerProfileKind_RefusesACallingCalendarThatDoesNotExist()
    {
        // Arrange
        var kind = (AIRecordKind<DialerProfile>)GetKinds(typeof(DialerAIToolsStartup))[ContactCenterRecordKinds.DialerProfileKind];
        var changed = kind.Copy(_harness.DialerProfiles.Entries.Single());
        changed.CallingCalendarId = "missing";

        // Act
        var result = await kind.ValidateAsync(_harness.Build(), changed, CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Errors);
        Assert.Contains(nameof(DialerProfile.CallingCalendarId), error.MemberNames);
        Assert.Contains("businessHoursCalendar", error.ErrorMessage);
    }

    [Fact]
    public async Task DialerProfileKind_RefusesACallerIdThatIsNotOneOfTheBusinessesPhoneNumbers()
    {
        // Arrange: the editor picks the caller ID from the addresses with the phone capability.
        _harness.Addresses.Entries.Add(new OmnichannelChannelEndpoint { ItemId = "main", DisplayText = "Main line", Value = "+12015550123", Capabilities = ["Phone"] });
        _harness.Addresses.Entries.Add(new OmnichannelChannelEndpoint { ItemId = "texts", DisplayText = "Texts", Value = "+12015550124", Capabilities = ["SMS"] });
        _harness.Configure = services => services.AddScoped<IOmnichannelChannelEndpointManager, OmnichannelChannelEndpointManager>();

        var kind = (AIRecordKind<DialerProfile>)GetKinds(typeof(DialerAIToolsStartup))[ContactCenterRecordKinds.DialerProfileKind];
        var services = _harness.Build();
        var typed = kind.Copy(_harness.DialerProfiles.Entries.Single());
        typed.CallerId = "+12015550199";
        var smsOnly = kind.Copy(_harness.DialerProfiles.Entries.Single());
        smsOnly.CallerId = "+12015550124";
        var owned = kind.Copy(_harness.DialerProfiles.Entries.Single());
        owned.CallerId = "+12015550123";

        // Act
        var typedResult = await kind.ValidateAsync(services, typed, CancellationToken.None);
        var smsOnlyResult = await kind.ValidateAsync(services, smsOnly, CancellationToken.None);
        var ownedResult = await kind.ValidateAsync(services, owned, CancellationToken.None);

        // Assert
        var error = Assert.Single(typedResult.Errors);
        Assert.Contains(nameof(DialerProfile.CallerId), error.MemberNames);
        Assert.Contains("+12015550123", error.ErrorMessage);
        Assert.DoesNotContain("+12015550124", error.ErrorMessage);
        Assert.False(smsOnlyResult.Succeeded);
        Assert.True(ownedResult.Succeeded, string.Join(" ", ownedResult.Errors.Select(item => item.ErrorMessage)));
    }

    [Fact]
    public async Task DialerProfileKind_KeepsACallerIdTheProfileAlreadyHad()
    {
        // Arrange: as on the page, a number the profile already shows is kept even when it is no longer an address.
        _harness.DialerProfiles.Entries.Single().CallerId = "+12015550188";
        _harness.Configure = services => services.AddScoped<IOmnichannelChannelEndpointManager, OmnichannelChannelEndpointManager>();

        var kind = (AIRecordKind<DialerProfile>)GetKinds(typeof(DialerAIToolsStartup))[ContactCenterRecordKinds.DialerProfileKind];
        var changed = kind.Copy(_harness.DialerProfiles.Entries.Single());
        changed.MaxAttempts = 4;

        // Act
        var result = await kind.ValidateAsync(_harness.Build(), changed, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(item => item.ErrorMessage)));
    }

    [Fact]
    public async Task DialerProfileKind_KeepsACalendarTheProfileAlreadyHad()
    {
        // Arrange: the profile's calendar was deleted after it was chosen; an unrelated change can still be saved.
        _harness.DialerProfiles.Entries.Single().CallingCalendarId = "deleted";

        var kind = (AIRecordKind<DialerProfile>)GetKinds(typeof(DialerAIToolsStartup))[ContactCenterRecordKinds.DialerProfileKind];
        var changed = kind.Copy(_harness.DialerProfiles.Entries.Single());
        changed.MaxAttempts = 4;

        // Act
        var result = await kind.ValidateAsync(_harness.Build(), changed, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.ErrorMessage)));
    }

    [Fact]
    public async Task DialerProfileKind_RefusesToRelaxTheComplianceRules()
    {
        // Arrange: the generic record tools cannot ask for approval, and the calls a relaxed profile allows cannot be
        // taken back, so the change stays on the admin page.
        var kind = (AIRecordKind<DialerProfile>)GetKinds(typeof(DialerAIToolsStartup))[ContactCenterRecordKinds.DialerProfileKind];
        var changed = kind.Copy(_harness.DialerProfiles.Entries.Single());
        changed.RespectDoNotCall = false;
        changed.EnforceCallingWindow = false;

        // Act
        var result = await kind.ValidateAsync(_harness.Build(), changed, CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Errors);
        Assert.Contains("do-not-call lists no longer respected", error.ErrorMessage);
        Assert.Contains("the calling window no longer enforced", error.ErrorMessage);
        Assert.Contains("admin page", error.ErrorMessage);
    }

    [Fact]
    public void DialerProfileKind_AllowsStricterRules_AndANewProfileThatRespectsDoNotCall()
    {
        // Arrange
        var before = new DialerProfile { RespectDoNotCall = true, EnforceAbandonmentCap = true, MaxAbandonmentRatePercent = 3 };

        // Act
        var stricter = ContactCenterRecordKinds.GetRelaxedCompliance(before, new DialerProfile { RespectDoNotCall = true, EnforceAbandonmentCap = true, MaxAbandonmentRatePercent = 2, EnforceCallingWindow = true });
        var higherCap = ContactCenterRecordKinds.GetRelaxedCompliance(before, new DialerProfile { RespectDoNotCall = true, EnforceAbandonmentCap = true, MaxAbandonmentRatePercent = 5 });
        var newProfile = ContactCenterRecordKinds.GetRelaxedCompliance(null, new DialerProfile { RespectDoNotCall = true });
        var newWithoutDoNotCall = ContactCenterRecordKinds.GetRelaxedCompliance(null, new DialerProfile { RespectDoNotCall = false });

        // Assert
        Assert.Empty(stricter);
        Assert.Equal("a higher abandonment rate cap", Assert.Single(higherCap));
        Assert.Empty(newProfile);
        Assert.Equal("do-not-call lists no longer respected", Assert.Single(newWithoutDoNotCall));
    }

    [Fact]
    public async Task DialerProfileKind_KeepsTheRulesOfTheDialerProfileHandler()
    {
        // Arrange
        _harness.Features.Remove(ContactCenterConstants.Feature.DialerPaced);

        var kind = (AIRecordKind<DialerProfile>)GetKinds(typeof(DialerAIToolsStartup))[ContactCenterRecordKinds.DialerProfileKind];

        // Act
        var result = await kind.ValidateAsync(_harness.Build(), new DialerProfile { Name = "Power", Mode = DialerMode.Power }, CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("Paced Dialing", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeleteContactCenterRecord_LeavesTheWritableKindsToDeleteRecord()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteContactCenterRecordTool(), _harness.Build(), new()
        {
            ["kind"] = "dialerProfile",
            ["id"] = "evening",
        });

        // Assert
        Assert.Equal("Nothing was deleted: kind must be one of queue, entryPoint, businessHoursCalendar, voiceMedia.", result);
        Assert.Single(_harness.DialerProfiles.Entries);
    }

    // Every field the kind returns is an argument of the tool that writes the record, so what getRecord returns can be
    // given back to that tool; the fields named as read-only are shown but set elsewhere.
    private static void AssertArgumentsOf(AIFunction tool, JsonElement projected, params string[] readOnlyFields)
    {
        var arguments = tool.JsonSchema.GetProperty("properties");

        foreach (var field in projected.EnumerateObject())
        {
            if (readOnlyFields.Contains(field.Name, StringComparer.Ordinal))
            {
                continue;
            }

            Assert.True(arguments.TryGetProperty(field.Name, out _), $"{tool.Name} has no argument named '{field.Name}'.");
        }
    }

    private static IReadOnlyDictionary<string, AIRecordKind> GetKinds(Type startupType)
    {
        var services = new ServiceCollection();
        var localizer = Activator.CreateInstance(typeof(RecordTestLocalizer<>).MakeGenericType(startupType));
        var startup = (StartupBase)Activator.CreateInstance(startupType, localizer);

        startup.ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds;
    }
}
