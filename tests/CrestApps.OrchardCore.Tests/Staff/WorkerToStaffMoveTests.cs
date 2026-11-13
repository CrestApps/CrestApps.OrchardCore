using CrestApps.Core;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Tests.Workforce;
using CrestApps.OrchardCore.Workforce.Migrations;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Staff;

/// <summary>
/// Workers stored by Workforce before Staff existed become staff members with the same id: the employment fields carry
/// over, the manager worker id becomes the manager, Workforce's own hours and availability go to its settings, and pay
/// rates and the kiosk PIN go to their new homes.
/// </summary>
public sealed class WorkerToStaffMoveTests
{
    private const string LegacyWorker = """
        {
          "ItemId": "w1",
          "DisplayName": "Lee Park",
          "UserId": "lee",
          "Email": "lee@example.com",
          "EmployeeNumber": "E-9",
          "JobTitle": "Plumber",
          "EmploymentType": 1,
          "Status": 1,
          "HireDate": "2024-02-01",
          "ManagerWorkerId": "w0",
          "PrimaryTeamId": "t1",
          "TeamMemberships": [ { "TeamId": "t1", "Role": 3, "EffectiveFrom": "2024-02-01" } ],
          "TimeZoneId": "America/Chicago",
          "HomeBase": { "Address": "1 Main St", "Latitude": 41.8, "Longitude": -87.6 },
          "ContractedHoursPerWeek": 32,
          "MinHoursPerWeek": 20,
          "MaxHoursPerWeek": 36,
          "MaxHoursPerDay": 9,
          "Availability": [ { "Day": 1, "Start": "09:00:00", "End": "17:00:00", "Preference": 2 } ],
          "CreatedUtc": "2024-02-01T10:00:00Z",
          "Properties": {
            "WorkerPayRates": { "Rates": [ { "EffectiveFrom": "2024-02-01", "HourlyRate": 31.5 } ] },
            "WorkerTimeClockSettings": { "PinHash": "hash", "PinSalt": "salt", "FailedAttempts": 2 },
            "WorkerPortalSettings": { "ReminderMinutes": 30 }
          }
        }
        """;

    [Fact]
    public async Task ALegacyWorker_BecomesAStaffMember_WithItsIdAndEveryValueInItsNewHome()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var serializer = harness.Services.GetRequiredService<IStore>().Configuration.ContentSerializer;

        // Act
        var staff = WorkforceWorkersMove.Convert(LegacyWorker, serializer);

        // Assert
        Assert.Equal("w1", staff.ItemId);
        Assert.Equal("Lee Park", staff.DisplayName);
        Assert.Equal("lee", staff.UserId);
        Assert.Equal("E-9", staff.EmployeeNumber);
        Assert.Equal(EmploymentType.PartTime, staff.EmploymentType);
        Assert.Equal(StaffStatus.OnLeave, staff.Status);
        Assert.Equal(new DateOnly(2024, 2, 1), staff.HireDate);
        Assert.Equal("w0", staff.ManagerId);
        Assert.Equal(TeamMemberRole.Manager, Assert.Single(staff.TeamMemberships).Role);
        Assert.Equal("America/Chicago", staff.TimeZoneId);
        Assert.Equal(41.8, staff.HomeBase.Latitude);
        Assert.Equal(32m, staff.ContractedHoursPerWeek);
        Assert.Equal(20m, staff.MinHoursPerWeek);
        Assert.Equal(36m, staff.MaxHoursPerWeek);
        Assert.Equal(9m, staff.MaxHoursPerDay);
        Assert.Equal(DayOfWeek.Monday, Assert.Single(staff.Availability).Day);
        Assert.True(staff.TryGet<StaffPayRates>(out var pay));
        Assert.Equal(31.5m, pay.RateOn(new DateOnly(2025, 1, 1)));
        Assert.True(staff.TryGet<StaffBadge>(out var badge));
        Assert.Equal("hash", badge.PinHash);
        Assert.Equal(2, badge.FailedAttempts);
        Assert.True(staff.TryGet<WorkerPortalSettings>(out var portal));
        Assert.Equal(30, portal.ReminderMinutes);
    }
}
