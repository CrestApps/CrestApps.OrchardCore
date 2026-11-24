using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Workforce;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Staff;

/// <summary>
/// A person's staff record, from their user account: made from the user editor with the user's name and e-mail, kept in
/// step when the user changes, joined to the team chosen there, and made on demand for modules such as Contact Center.
/// </summary>
public sealed class StaffAccountsTests
{
    [Fact]
    public async Task AStaffMember_IsMadeFromTheUser_WithTheirNameAndEmail_AndJoinsTheTeamChosen()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Support", TeamKind.Team);

        Users(harness).Set("ana", "Ana Lopez", "ana@example.com");

        // Act
        var saved = await SaveAsync(harness, new StaffAccountInput
        {
            EmployeeNumber = "E-100",
            JobTitle = "Agent",
            EmploymentType = EmploymentType.PartTime,
            HireDate = new DateOnly(2026, 3, 1),
            PrimaryTeamId = team.ItemId,
            ContractedHoursPerWeek = 20,
        });
        var staff = await FindAsync(harness);

        // Assert
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        Assert.True(saved.Created);
        Assert.Equal("Ana Lopez", staff.DisplayName);
        Assert.Equal("ana@example.com", staff.Email);
        Assert.Equal("E-100", staff.EmployeeNumber);
        Assert.Equal(EmploymentType.PartTime, staff.EmploymentType);
        Assert.Equal(20m, staff.ContractedHoursPerWeek);
        Assert.Equal(team.ItemId, staff.PrimaryTeamId);
        Assert.Equal(team.ItemId, Assert.Single(staff.TeamMemberships).TeamId);
    }

    [Fact]
    public async Task SavingAgain_ChangesTheRecord_AndANewTeamIsAdded_KeepingTheOld()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var support = await WorkforceTests.AddTeamAsync(harness, "Support", TeamKind.Team);
        var sales = await WorkforceTests.AddTeamAsync(harness, "Sales", TeamKind.Team);

        // Act
        await SaveAsync(harness, new StaffAccountInput { PrimaryTeamId = support.ItemId });
        var again = await SaveAsync(harness, new StaffAccountInput { JobTitle = "Team lead", Status = StaffStatus.OnLeave, PrimaryTeamId = sales.ItemId });
        var staff = await FindAsync(harness);
        var count = await harness.RunAsync(async services => (await services.GetRequiredService<IStaffMemberManager>().GetAllAsync()).Count(), commit: false);

        // Assert
        Assert.True(again.Succeeded, string.Join(" ", again.Errors));
        Assert.False(again.Created);
        Assert.Equal(1, count);
        Assert.Equal("Team lead", staff.JobTitle);
        Assert.Equal(StaffStatus.OnLeave, staff.Status);
        Assert.Equal(sales.ItemId, staff.PrimaryTeamId);
        Assert.Equal([support.ItemId, sales.ItemId], staff.TeamMemberships.Select(membership => membership.TeamId));
    }

    [Fact]
    public async Task ALinkedStaffMember_TakesTheUsersNewNameAndEmail_WhenSaved()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();

        Users(harness).Set("ana", "Ana Lopez", "ana@example.com");
        await SaveAsync(harness, new StaffAccountInput());
        Users(harness).Set("ana", "Ana López-Ruiz", "ana.ruiz@example.com");

        // Act
        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IStaffMemberManager>();

            await manager.UpdateAsync(await manager.FindByUserIdAsync("ana"));
        });
        var staff = await FindAsync(harness);

        // Assert
        Assert.Equal("Ana López-Ruiz", staff.DisplayName);
        Assert.Equal("ana.ruiz@example.com", staff.Email);
    }

    [Fact]
    public async Task EnsureForUser_MakesOneRecordWithTheDefaults_AndOnlyAddsCapabilitiesAfterwards()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();

        // Act
        var first = await harness.RunAsync(services => services.GetRequiredService<StaffAccounts>().EnsureForUserAsync("ana", "Ana", ["Agent"], staff => staff.JobTitle = "Agent"));
        var second = await harness.RunAsync(services => services.GetRequiredService<StaffAccounts>().EnsureForUserAsync("ana", "Ana", ["Agent", "Cashier"], staff => staff.JobTitle = "Changed"));
        var staff = await FindAsync(harness);

        // Assert
        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal("Agent", staff.JobTitle);
        Assert.Equal(["Agent", "Cashier"], staff.Capabilities);
    }

    private static InMemoryUserDirectory Users(CalendarTestHarness harness)
        => harness.Services.GetRequiredService<InMemoryUserDirectory>();

    private static Task<StaffAccountResult> SaveAsync(CalendarTestHarness harness, StaffAccountInput input)
        => harness.RunAsync(services => services.GetRequiredService<StaffAccounts>().SaveAsync("ana", "Ana Lopez", input));

    private static Task<StaffMember> FindAsync(CalendarTestHarness harness)
        => harness.RunAsync(services => services.GetRequiredService<IStaffMemberManager>().FindByUserIdAsync("ana"), commit: false);
}
