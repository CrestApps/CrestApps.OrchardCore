using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// A person's worker, from their user account: made from the user editor with the user's name and email, kept in
/// step when the user changes, and joined to the team chosen there.
/// </summary>
public sealed class WorkerAccountsTests
{
    [Fact]
    public async Task AWorker_IsMadeFromTheUser_WithTheirNameAndEmail_AndJoinsTheTeamChosen()
    {
        await using var harness = await WorkforceTests.CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Support", TeamKind.Team);

        var saved = await SaveAsync(harness, new UserWorkerInput
        {
            EmployeeNumber = "E-100",
            JobTitle = "Agent",
            EmploymentType = EmploymentType.PartTime,
            HireDate = new DateOnly(2026, 3, 1),
            PrimaryTeamId = team.ItemId,
            ContractedHoursPerWeek = 20,
        });
        var worker = await FindAsync(harness);

        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        Assert.Equal("Ana Lopez", worker.DisplayName);
        Assert.Equal("ana@example.com", worker.Email);
        Assert.Equal("E-100", worker.EmployeeNumber);
        Assert.Equal(EmploymentType.PartTime, worker.EmploymentType);
        Assert.Equal(20m, worker.ContractedHoursPerWeek);
        Assert.Equal(team.ItemId, worker.PrimaryTeamId);
        Assert.Equal(team.ItemId, Assert.Single(worker.TeamMemberships).TeamId);
    }

    [Fact]
    public async Task SavingAgain_ChangesTheWorker_AndANewTeamIsAdded_KeepingTheOld()
    {
        await using var harness = await WorkforceTests.CreateAsync();
        var support = await WorkforceTests.AddTeamAsync(harness, "Support", TeamKind.Team);
        var sales = await WorkforceTests.AddTeamAsync(harness, "Sales", TeamKind.Team);

        await SaveAsync(harness, new UserWorkerInput { PrimaryTeamId = support.ItemId });
        var again = await SaveAsync(harness, new UserWorkerInput { JobTitle = "Team lead", Status = WorkerStatus.OnLeave, PrimaryTeamId = sales.ItemId });
        var worker = await FindAsync(harness);
        var count = await harness.RunAsync(async services => (await services.GetRequiredService<IWorkerManager>().GetAllAsync()).Count(), commit: false);

        Assert.True(again.Succeeded, string.Join(" ", again.Errors));
        Assert.Equal(1, count);
        Assert.Equal("Team lead", worker.JobTitle);
        Assert.Equal(WorkerStatus.OnLeave, worker.Status);
        Assert.Equal(sales.ItemId, worker.PrimaryTeamId);
        Assert.Equal([support.ItemId, sales.ItemId], worker.TeamMemberships.Select(membership => membership.TeamId));
    }

    [Fact]
    public async Task ALinkedWorker_TakesTheUsersNewNameAndEmail_AndAnUnlinkedOneIsLeftAlone()
    {
        await using var harness = await WorkforceTests.CreateAsync();

        await SaveAsync(harness, new UserWorkerInput());
        await WorkforceTests.AddWorkerAsync(harness, "Kiosk only");

        var changed = await harness.RunAsync(services => services.GetRequiredService<WorkerAccounts>().RefreshAsync("ana", "Ana López-Ruiz", "ana.ruiz@example.com"));
        var unchanged = await harness.RunAsync(services => services.GetRequiredService<WorkerAccounts>().RefreshAsync("ana", "Ana López-Ruiz", "ana.ruiz@example.com"));
        var nobody = await harness.RunAsync(services => services.GetRequiredService<WorkerAccounts>().RefreshAsync("someone", "Someone", "someone@example.com"));
        var worker = await FindAsync(harness);

        Assert.True(changed);
        Assert.False(unchanged);
        Assert.False(nobody);
        Assert.Equal("Ana López-Ruiz", worker.DisplayName);
        Assert.Equal("ana.ruiz@example.com", worker.Email);
    }

    private static Task<PlanningResult<WorkerProfile>> SaveAsync(Calendars.CalendarTestHarness harness, UserWorkerInput input)
        => harness.RunAsync(services => services.GetRequiredService<WorkerAccounts>().SaveAsync("ana", "Ana Lopez", "ana@example.com", input));

    private static Task<WorkerProfile> FindAsync(Calendars.CalendarTestHarness harness)
        => harness.RunAsync(services => services.GetRequiredService<IWorkerManager>().FindByUserIdAsync("ana"), commit: false);
}
