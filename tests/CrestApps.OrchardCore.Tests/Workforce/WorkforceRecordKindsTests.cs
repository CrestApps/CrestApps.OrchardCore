using System.Text.Json;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Tests.Agent.Records;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The Workforce setup catalogs are exposed to the generic record tools as read-only kinds, with the permission of their
/// admin page, allow-listed fields and, for rotations and coverage requirements, the teams the caller manages.
/// </summary>
public sealed class WorkforceRecordKindsTests
{
    public static TheoryData<Type, string, string, string> Kinds => new()
    {
        { typeof(WorkforceRecordKindsStartup), "activityCode", WorkforcePermissions.ManageActivityCodes.Name, WorkforceConstants.Features.Workforce },
        { typeof(ShiftPlanningRecordKindsStartup), "shiftTemplate", WorkforcePermissions.ManageShiftTemplates.Name, WorkforceConstants.Features.ShiftPlanning },
        { typeof(ShiftPlanningRecordKindsStartup), "rotation", WorkforcePermissions.ManageSchedules.Name, WorkforceConstants.Features.ShiftPlanning },
        { typeof(ShiftPlanningRecordKindsStartup), "coverageRequirement", WorkforcePermissions.ManageSchedules.Name, WorkforceConstants.Features.ShiftPlanning },
        { typeof(TimeOffRecordKindsStartup), "timeOffType", WorkforcePermissions.ManageTimeOffPolicies.Name, WorkforceConstants.Features.TimeOff },
        { typeof(TimeOffRecordKindsStartup), "timeOffPolicy", WorkforcePermissions.ManageTimeOffPolicies.Name, WorkforceConstants.Features.TimeOff },
        { typeof(TimeOffRecordKindsStartup), "timeOffAllotment", WorkforcePermissions.ManageTimeOffPolicies.Name, WorkforceConstants.Features.TimeOff },
        { typeof(TimeOffRecordKindsStartup), "blackoutPeriod", WorkforcePermissions.ManageTimeOffPolicies.Name, WorkforceConstants.Features.TimeOff },
        { typeof(TimeClockRecordKindsStartup), "timeClockPolicy", WorkforcePermissions.ManageTimeClockPolicies.Name, WorkforceConstants.Features.TimeClock },
    };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Kind_IsRegisteredReadOnly_WithThePagesPermission_OnItsFeature(Type startupType, string name, string permission, string featureId)
    {
        var kinds = GetKinds(startupType);
        var feature = startupType.GetCustomAttributes(typeof(FeatureAttribute), inherit: false).Cast<FeatureAttribute>().Single();
        var required = startupType.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();

        Assert.True(kinds.TryGetValue(name, out var kind), $"The kind '{name}' is not registered.");
        Assert.False(kind.CanWrite);
        Assert.Equal(permission, kind.Permission.Name);
        Assert.Equal(permission, kind.EffectiveReadPermission.Name);
        Assert.False(string.IsNullOrWhiteSpace(kind.Description));
        Assert.NotEqual(kind.Name, kind.Title);
        Assert.Equal(featureId, feature.FeatureName);
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
    }

    [Fact]
    public void Startups_RegisterNoPayrollOrKioskKind()
    {
        var names = new[] { typeof(WorkforceRecordKindsStartup), typeof(ShiftPlanningRecordKindsStartup), typeof(TimeOffRecordKindsStartup), typeof(TimeClockRecordKindsStartup) }
            .SelectMany(type => GetKinds(type).Keys)
            .ToList();

        Assert.Equal(9, names.Count);
        Assert.DoesNotContain("payCode", names);
        Assert.DoesNotContain("payPeriodCalendar", names);
        Assert.DoesNotContain("payrollCsvColumns", names);
        Assert.DoesNotContain("timeClockKiosk", names);
    }

    [Fact]
    public void Projections_LeaveOutPropertiesAndPayFields()
    {
        var activity = new ActivityCode { ItemId = "a1", Name = "Meal" };
        var type = new TimeOffType { ItemId = "t1", Name = "Vacation", PayCode = "VAC" };
        var policy = new TimeClockPolicy { ItemId = "p1", Name = "Standard", PayPeriodCalendarId = "calendar-1" };

        activity.Properties["Secret"] = "canary-activity";
        type.Properties["Secret"] = "canary-type";
        policy.Properties["Secret"] = "canary-policy";

        var activityJson = Project(GetKinds(typeof(WorkforceRecordKindsStartup))["activityCode"], activity);
        var typeJson = Project(GetKinds(typeof(TimeOffRecordKindsStartup))["timeOffType"], type);
        var policyJson = Project(GetKinds(typeof(TimeClockRecordKindsStartup))["timeClockPolicy"], policy);

        Assert.Contains("\"name\":\"Meal\"", activityJson, StringComparison.Ordinal);
        Assert.DoesNotContain("canary", activityJson + typeJson + policyJson, StringComparison.Ordinal);
        Assert.DoesNotContain("properties", activityJson + typeJson + policyJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VAC", typeJson, StringComparison.Ordinal);
        Assert.DoesNotContain("calendar-1", policyJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rotation_ListsAndFindsOnlyTheTeamsTheCallerManages()
    {
        await using var harness = await WorkforceTests.CreateAsync(services => services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>());
        var clinic = await WorkforceTests.AddTeamAsync(harness, "Clinic", TeamKind.Team);
        var lab = await WorkforceTests.AddTeamAsync(harness, "Lab", TeamKind.Team);

        await WorkforceTests.AddWorkerAsync(harness, "Maya", worker =>
        {
            worker.UserId = "maya";
            worker.TeamMemberships.Add(new TeamMembership { TeamId = clinic.ItemId, Role = TeamMemberRole.Manager });
        });

        var clinicRotation = await AddRotationAsync(harness, "Clinic days", clinic);
        var labRotation = await AddRotationAsync(harness, "Lab nights", lab);
        var kind = (AIRecordKind<ShiftPattern>)GetKinds(typeof(ShiftPlanningRecordKindsStartup))["rotation"];

        var (listed, foundClinic, foundLab) = await harness.RunAsync(async services =>
        {
            services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
            {
                User = CalendarTestHarness.User("maya", WorkforcePermissions.ManageSchedules.Name),
                RequestServices = services,
            };

            var page = await kind.ListAsync(services, new AIRecordQuery(), CancellationToken.None);

            return (page, await kind.FindAsync(services, clinicRotation.ItemId, CancellationToken.None), await kind.FindAsync(services, labRotation.ItemId, CancellationToken.None));
        }, commit: false);

        Assert.Equal(1, listed.Count);
        Assert.Equal(clinicRotation.ItemId, Assert.Single(listed.Entries).ItemId);
        Assert.NotNull(foundClinic);
        Assert.Null(foundLab);
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

    private static string Project(AIRecordKind kind, object record)
    {
        var projection = kind switch
        {
            AIRecordKind<ActivityCode> typed => typed.Projection((ActivityCode)record),
            AIRecordKind<TimeOffType> typed => typed.Projection((TimeOffType)record),
            AIRecordKind<TimeClockPolicy> typed => typed.Projection((TimeClockPolicy)record),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind.Name, "Unexpected kind."),
        };

        return JsonSerializer.Serialize(projection);
    }

    private static Task<ShiftPattern> AddRotationAsync(CalendarTestHarness harness, string name, Team team)
    {
        return harness.RunAsync(async services =>
        {
            var patterns = services.GetRequiredService<IShiftPatternManager>();
            var pattern = await patterns.NewAsync();

            pattern.Name = name;
            pattern.TeamId = team.ItemId;
            await patterns.CreateAsync(pattern);

            return pattern;
        });
    }
}
