using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Staff.Core.Handlers;
using CrestApps.OrchardCore.Staff.Drivers;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Tests.Workforce;
using CrestApps.OrchardCore.Workforce.Migrations;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Staff;

/// <summary>
/// The owners gain what Payroll needs: dated salaries, a pay basis and a currency on Staff Compensation, every pay
/// change in the staff history, amounts typed with the invariant culture, and a team's own structured address that
/// takes over Workforce's one-line address.
/// </summary>
public sealed class StaffCompensationTests
{
    [Fact]
    public void SalaryOn_ReadsTheLatestStartedSalary_ElseTheUndatedOne()
    {
        // Arrange
        var pay = new StaffPayRates
        {
            PayBasis = StaffPayBasis.Salary,
            AnnualSalary = 40000m,
            Salaries =
            [
                new StaffSalary { EffectiveFrom = new DateOnly(2026, 1, 1), AnnualAmount = 50000m },
                new StaffSalary { EffectiveFrom = new DateOnly(2026, 7, 1), AnnualAmount = 54000m },
            ],
        };

        // Act + Assert
        Assert.Equal(40000m, pay.SalaryOn(new DateOnly(2025, 12, 31)));
        Assert.Equal(50000m, pay.SalaryOn(new DateOnly(2026, 6, 30)));
        Assert.Equal(54000m, pay.SalaryOn(new DateOnly(2026, 11, 13)));
    }

    [Fact]
    public void PaySummary_WritesBasisCurrencyRatesSalariesAndCost_WithTheInvariantCulture()
    {
        // Arrange
        var staff = new StaffMember();
        staff.Put(new StaffPayRates
        {
            PayBasis = StaffPayBasis.Hourly,
            Currency = "USD",
            HourlyCost = 38.2m,
            Rates = [new StaffPayRate { EffectiveFrom = new DateOnly(2026, 1, 1), HourlyRate = 25.5m }],
            Salaries = [new StaffSalary { EffectiveFrom = new DateOnly(2026, 1, 1), AnnualAmount = 52000m }],
        });

        // Act
        var summary = StaffSavedState.PaySummary(staff);

        // Assert
        Assert.Equal("Hourly USD; rate 2026-01-01 25.5; salary 2026-01-01 52000; cost 38.2", summary);
        Assert.Equal(string.Empty, StaffSavedState.PaySummary(new StaffMember()));
    }

    [Theory]
    [InlineData("25.50", "25.5")]
    [InlineData(" 1200 ", "1200")]
    [InlineData("0.12345", "0.1235")]
    public void PayAmounts_AreReadAndWrittenWithTheInvariantCulture(string typed, string shown)
    {
        // Act
        var value = StaffPayDisplayDriver.Parse(typed);

        // Assert
        Assert.Equal(shown, StaffPayDisplayDriver.Text(value));
    }

    [Theory]
    [InlineData("25,50")]
    [InlineData("abc")]
    [InlineData("1,200.00")]
    public void PayAmounts_WithACommaOrText_AreRefused(string typed)
        => Assert.Null(StaffPayDisplayDriver.Parse(typed));

    [Fact]
    public async Task APayChange_IsKeptInTheHistory()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var staff = await WorkforceTests.AddWorkerAsync(harness, "Lee Park", member => member.Put(new StaffPayRates { Rates = [new StaffPayRate { EffectiveFrom = new DateOnly(2026, 1, 1), HourlyRate = 25m }] }));

        // Act
        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IStaffMemberManager>();
            var stored = await manager.FindByIdAsync(staff.ItemId);
            var pay = stored.TryGet<StaffPayRates>(out var rates) ? rates : new StaffPayRates();

            pay.Rates.Add(new StaffPayRate { EffectiveFrom = new DateOnly(2026, 7, 1), HourlyRate = 27m });
            stored.Put(pay);
            await manager.UpdateAsync(stored);
        });
        var history = await harness.RunAsync(services => services.GetRequiredService<IStaffHistoryService>().ListAsync(staff.ItemId), commit: false);

        // Assert
        var change = Assert.Single(history, entry => entry.Field == StaffHistoryFields.Pay);
        Assert.Equal("Hourly; rate 2026-01-01 25", change.From);
        Assert.Equal("Hourly; rate 2026-01-01 25; rate 2026-07-01 27", change.To);
    }

    [Fact]
    public void TeamAddressMove_LiftsTheOneLineAddress_AndKeepsTheGeofencePoint()
    {
        // Arrange
        var team = new Team { Name = "Boston" };
        team.Properties[nameof(WorkforceTeamSettings)] = new JsonObject
        {
            ["Location"] = new JsonObject { ["Address"] = "1 Main St, Boston", ["Latitude"] = 42.36, ["Longitude"] = -71.06 },
            ["TimeClockPolicyId"] = "policy",
        };

        // Act
        var changed = WorkforceTeamAddressMove.Lift(team);
        var settings = team.GetWorkforceSettings();

        // Assert
        Assert.True(changed);
        Assert.Equal("1 Main St, Boston", team.Address.AddressLine1);
        Assert.Equal(42.36, settings.Location.Latitude);
        Assert.Equal("policy", settings.TimeClockPolicyId);
        Assert.False(WorkforceTeamAddressMove.Lift(team));
    }

    [Fact]
    public void TeamAddressMove_KeepsAnAddressTheTeamAlreadyHas_AndDropsALocationThatOnlyHeldTheLine()
    {
        // Arrange
        var team = new Team { Name = "Denver", Address = new Address { AddressLine1 = "5 Pine Ave", City = "Denver", Country = "US" } };
        team.Properties[nameof(WorkforceTeamSettings)] = new JsonObject
        {
            ["Location"] = new JsonObject { ["Address"] = "old line" },
        };

        // Act
        var changed = WorkforceTeamAddressMove.Lift(team);

        // Assert
        Assert.True(changed);
        Assert.Equal("5 Pine Ave", team.Address.AddressLine1);
        Assert.Null(team.GetWorkforceSettings().Location);
    }
}
