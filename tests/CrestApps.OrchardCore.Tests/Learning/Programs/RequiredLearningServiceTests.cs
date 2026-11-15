using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Programs;
using CrestApps.OrchardCore.Learning.Programs.Core.Services;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Programs;

/// <summary>
/// Required learning: a run requires the course of the audience's people with a due date and enrolls them, a
/// completed course completes the state, the nearest reminder is sent once, an overdue state is marked, an exemption
/// holds, and a person who leaves the audience is no longer required.
/// </summary>
public sealed class RequiredLearningServiceTests
{
    [Fact]
    public async Task RunAsync_RequiresTheCourseWithADueDate_AndStatesFollowCompletionRemindersOverdueExemptionAndLeaving()
    {
        // Arrange
        var h = await ProgramsHarness.CreateAsync();

        h.Audiences["field"] = ["ana", "ben", "cid"];

        var requirement = new LearningRequirement
        {
            Name = "Safety basics",
            TargetKind = ProgramsConstants.TargetKinds.Course,
            TargetId = "c1",
            AudienceName = "field",
            DueRule = RequiredLearningDueRule.DaysAfterRequired,
            DueDays = 10,
            ReminderDays = [7, 1, 14],
            EscalateAfterDays = 2,
        };
        var errors = await h.RunAsync(services => services.GetRequiredService<IRequiredLearningService>().SaveAsync(requirement));

        // Act: the first run requires it of the three people.
        var first = await h.RunAsync(services => services.GetRequiredService<IRequiredLearningService>().RunAsync(requirement.ItemId, "admin"));
        var enrolled = await h.EnrollmentsAsync("ben", "c1");

        // Ana completes the course: her state completes through the enrollment events.
        await h.CompleteCourseAsync("ana", "c1");

        // Four days later the 7-day reminder (and the 14-day one it passed) is due; a second pass sends nothing more.
        h.Clock.UtcNow = h.Clock.UtcNow.AddDays(4);
        var reminders = await h.RunAsync(services => services.GetRequiredService<IRequiredLearningService>().SendRemindersAsync());
        var remindersAgain = await h.RunAsync(services => services.GetRequiredService<IRequiredLearningService>().SendRemindersAsync());

        // Cid is exempted.
        var cid = await h.RunAsync(async services => (await services.GetRequiredService<IRequiredLearningService>().PageAsync(requirement.ItemId, null, 0, 10)).Items.Single(item => item.UserId == "cid"));
        var exempt = await h.RunAsync(services => services.GetRequiredService<IRequiredLearningService>().ExemptAsync(cid.ItemId, new RequiredLearningExemption { Reason = "Certified elsewhere", GrantedBy = "admin" }));

        // After the due date Ben is overdue.
        h.Clock.UtcNow = h.Clock.UtcNow.AddDays(8);
        await h.RunAsync(services => services.GetRequiredService<IRequiredLearningService>().SendRemindersAsync());

        // Ben leaves the audience and Dee joins it.
        h.Audiences["field"] = ["ana", "cid", "dee"];
        var second = await h.RunAsync(services => services.GetRequiredService<IRequiredLearningService>().RunAsync(requirement.ItemId, "admin"));
        var states = await h.RunAsync(async services => (await services.GetRequiredService<IRequiredLearningService>().PageAsync(requirement.ItemId, null, 0, 10)).Items.ToDictionary(item => item.UserId));
        var saved = await h.RunAsync(services => services.GetRequiredService<IRequiredLearningService>().FindByIdAsync(requirement.ItemId));

        // Assert
        Assert.Empty(errors);
        Assert.Equal(new RequiredLearningRunResult(3, 3, 0, 0), first);
        Assert.Equal([14, 7, 1], saved.ReminderDays);
        Assert.Equal(ProgramsConstants.RequirementSource, Assert.Single(Assert.Single(enrolled).Grants).Source);
        Assert.Equal(LearningTestHarness.Now.Date.AddDays(11).AddSeconds(-1), Assert.Single(enrolled).DueUtc);
        Assert.Equal(2, reminders);
        Assert.Equal(0, remindersAgain);
        Assert.Contains("due-soon:ben:7", h.Observer.Events);
        Assert.DoesNotContain("due-soon:ben:14", h.Observer.Events);
        Assert.Contains("completed:ana", h.Observer.Events);
        Assert.Contains("overdue:ben", h.Observer.Events);
        Assert.Equal(RequiredLearningState.Exempt, exempt.State);
        Assert.Equal(1, second.Added);
        Assert.Equal(1, second.Removed);
        Assert.Equal(RequiredLearningState.Completed, states["ana"].State);
        Assert.Equal(RequiredLearningState.NotRequired, states["ben"].State);
        Assert.Equal(RequiredLearningState.Exempt, states["cid"].State);
        Assert.Equal(RequiredLearningState.Required, states["dee"].State);
        Assert.Equal(3, saved.LastRunCovered);
    }

    [Fact]
    public async Task SaveAsync_RefusesAMissingTargetAudienceAndDueDate()
    {
        // Arrange
        var h = await ProgramsHarness.CreateAsync();

        // Act
        var errors = await h.RunAsync(services => services.GetRequiredService<IRequiredLearningService>().SaveAsync(new LearningRequirement
        {
            Name = "Broken",
            TargetKind = ProgramsConstants.TargetKinds.Course,
            TargetId = "missing",
            DueRule = RequiredLearningDueRule.FixedDate,
        }));

        // Assert
        Assert.Equal(3, errors.Count);
    }

    [Theory]
    [InlineData(RequiredLearningState.Required, false, null, 5, RequiredLearningState.Required)]
    [InlineData(RequiredLearningState.Required, true, null, 5, RequiredLearningState.InProgress)]
    [InlineData(RequiredLearningState.InProgress, true, null, -1, RequiredLearningState.Overdue)]
    [InlineData(RequiredLearningState.Overdue, true, 1, -1, RequiredLearningState.Completed)]
    [InlineData(RequiredLearningState.Exempt, true, 1, -1, RequiredLearningState.Exempt)]
    public void StateFor_FollowsTheTarget(RequiredLearningState current, bool started, int? completedDaysAgo, int dueInDays, RequiredLearningState expected)
    {
        // Arrange
        var now = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
        var assignment = new RequiredLearningAssignment { State = current, DueUtc = now.AddDays(dueInDays) };
        var status = new RequiredLearningTargetStatus(started, completedDaysAgo is { } days ? now.AddDays(-days) : null);

        // Act
        var state = DefaultRequiredLearningService.StateFor(assignment, status, now);

        // Assert
        Assert.Equal(expected, state);
    }
}
