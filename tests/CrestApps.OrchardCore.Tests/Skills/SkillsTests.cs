using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Skills;

/// <summary>
/// The skill catalog and skill assignments on a real SQLite store: unique names, replacing a person's skills,
/// effective dates, required certifications, finding holders and the change handler.
/// </summary>
public sealed class SkillsTests
{
    private static readonly SkillSubject _ana = SkillSubject.User("ana");
    private static readonly SkillSubject _ben = SkillSubject.Worker("ben");

    [Fact]
    public async Task ValidateAsync_WhenTheNameIsTakenOrBlank_Fails()
    {
        // Arrange
        await using var harness = await SkillsTestHarness.CreateAsync();
        await harness.AddSkillAsync("Spanish");

        // Act
        var (duplicate, blank) = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ISkillManager>();
            var copy = await manager.NewAsync();
            var empty = await manager.NewAsync();

            copy.Name = " spanish ";

            return (await manager.ValidateAsync(copy), await manager.ValidateAsync(empty));
        }, commit: false);

        // Assert
        Assert.False(duplicate.Succeeded);
        Assert.False(blank.Succeeded);
    }

    [Fact]
    public async Task CreateAsync_StampsTheTimes_AndTrimsTheName()
    {
        // Arrange
        await using var harness = await SkillsTestHarness.CreateAsync();

        // Act
        var skill = await harness.AddSkillAsync("  Boilers  ");
        var found = await harness.RunAsync(services => services.GetRequiredService<ISkillManager>().FindByNameAsync("Boilers"), commit: false);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(skill.ItemId, found.ItemId);
        Assert.Equal(SkillsTestHarness.Now, found.CreatedUtc);
    }

    [Fact]
    public async Task SetAsync_ReplacesThePersonsSkills_AndTellsTheHandlersWhatChanged()
    {
        // Arrange
        var handler = new RecordingAssignmentHandler();
        await using var harness = await SkillsTestHarness.CreateAsync(services => services.AddSingleton<ISkillAssignmentHandler>(handler));
        var spanish = await harness.AddSkillAsync("Spanish");
        var french = await harness.AddSkillAsync("French");
        var billing = await harness.AddSkillAsync("Billing");

        await SetAsync(harness, _ana, (spanish.ItemId, 4), (french.ItemId, 2));
        handler.Contexts.Clear();

        // Act
        var result = await SetAsync(harness, _ana, (spanish.ItemId, 5), (billing.ItemId, 3));
        var assignments = await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().GetAssignmentsAsync(_ana), commit: false);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal(
            new[] { (billing.ItemId, 3), (spanish.ItemId, 5) }.OrderBy(a => a.Item1, StringComparer.Ordinal),
            assignments.Select(a => (a.SkillId, a.Proficiency)).OrderBy(a => a.SkillId, StringComparer.Ordinal));
        var context = Assert.Single(handler.Contexts);
        Assert.Equal(_ana, context.Subject);
        Assert.Equal([french.ItemId], context.RemovedSkillIds);
        Assert.Equal(2, context.Changed.Count);
        Assert.Equal(2, context.Assignments.Count);
        Assert.Equal("tester", context.Actor);
    }

    [Fact]
    public async Task SetAsync_WhenNothingChanged_DoesNotTellTheHandlers()
    {
        // Arrange
        var handler = new RecordingAssignmentHandler();
        await using var harness = await SkillsTestHarness.CreateAsync(services => services.AddSingleton<ISkillAssignmentHandler>(handler));
        var spanish = await harness.AddSkillAsync("Spanish");

        await SetAsync(harness, _ana, (spanish.ItemId, 4));
        handler.Contexts.Clear();

        // Act
        var result = await SetAsync(harness, _ana, (spanish.ItemId, 4));

        // Assert
        Assert.True(result.Succeeded);
        Assert.Empty(handler.Contexts);
    }

    [Fact]
    public async Task AssignAsync_RefusesAProficiencyOffTheScale_AnUnknownSkill_AndADisabledSkill()
    {
        // Arrange
        await using var harness = await SkillsTestHarness.CreateAsync();
        var spanish = await harness.AddSkillAsync("Spanish");
        var retired = await harness.AddSkillAsync("Telex", skill => skill.Enabled = false);

        // Act
        var tooHigh = await AssignAsync(harness, _ana, spanish.ItemId, 6);
        var unknown = await AssignAsync(harness, _ana, "missing", 3);
        var disabled = await AssignAsync(harness, _ana, retired.ItemId, 3);
        var backwards = await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().AssignAsync(_ana, new SkillAssignmentInput
        {
            SkillId = spanish.ItemId,
            EffectiveFrom = SkillsTestHarness.Today,
            EffectiveTo = SkillsTestHarness.Today.AddDays(-1),
        }, "tester"));
        var assignments = await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().GetAssignmentsAsync(_ana), commit: false);

        // Assert
        Assert.False(tooHigh.Succeeded);
        Assert.False(unknown.Succeeded);
        Assert.False(disabled.Succeeded);
        Assert.False(backwards.Succeeded);
        Assert.Empty(assignments);
    }

    [Fact]
    public async Task AssignAsync_ChangesTheExistingAssignment_InsteadOfAddingASecond()
    {
        // Arrange
        await using var harness = await SkillsTestHarness.CreateAsync();
        var spanish = await harness.AddSkillAsync("Spanish");

        await AssignAsync(harness, _ana, spanish.ItemId, 2);

        // Act
        await AssignAsync(harness, _ana, spanish.ItemId, 5);
        var assignments = await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().GetAssignmentsAsync(_ana), commit: false);

        // Assert
        var assignment = Assert.Single(assignments);
        Assert.Equal(5, assignment.Proficiency);
    }

    [Fact]
    public async Task GetEffectiveAsync_HonorsTheDates_AndTheRequiredCertification()
    {
        // Arrange
        await using var harness = await SkillsTestHarness.CreateAsync();
        var gas = await harness.AddCertificationAsync("Gas safety", certification => certification.ValidityMonths = 12);
        var boilers = await harness.AddSkillAsync("Boilers", skill => skill.RequiredCertificationId = gas.ItemId);
        var spanish = await harness.AddSkillAsync("Spanish");
        var french = await harness.AddSkillAsync("French");

        await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().SetAsync(_ben,
        [
            new SkillAssignmentInput { SkillId = boilers.ItemId, Proficiency = 4 },
            new SkillAssignmentInput { SkillId = spanish.ItemId, EffectiveFrom = SkillsTestHarness.Today.AddDays(7) },
            new SkillAssignmentInput { SkillId = french.ItemId, EffectiveTo = SkillsTestHarness.Today.AddDays(-1) },
        ], "tester"));

        // Act
        var withoutCertificate = await EffectiveSkillIdsAsync(harness, _ben, SkillsTestHarness.Today);
        await SaveRecordAsync(harness, _ben, gas.ItemId, SkillsTestHarness.Today.AddMonths(-1), CertificationStatus.Valid);
        var withCertificate = await EffectiveSkillIdsAsync(harness, _ben, SkillsTestHarness.Today);
        var nextWeek = await EffectiveSkillIdsAsync(harness, _ben, SkillsTestHarness.Today.AddDays(7));
        var afterExpiry = await EffectiveSkillIdsAsync(harness, _ben, SkillsTestHarness.Today.AddMonths(12));

        // Assert
        Assert.Empty(withoutCertificate);
        Assert.Equal([boilers.ItemId], withCertificate);
        Assert.Equal(new[] { boilers.ItemId, spanish.ItemId }.Order(StringComparer.Ordinal), nextWeek.Order(StringComparer.Ordinal));
        Assert.Equal([spanish.ItemId], afterExpiry);
    }

    [Fact]
    public async Task FindHoldersAsync_FiltersByProficiencyDayAndKind()
    {
        // Arrange
        await using var harness = await SkillsTestHarness.CreateAsync();
        var spanish = await harness.AddSkillAsync("Spanish");

        await AssignAsync(harness, SkillSubject.User("expert"), spanish.ItemId, 5);
        await AssignAsync(harness, SkillSubject.User("novice"), spanish.ItemId, 1);
        await AssignAsync(harness, SkillSubject.Worker("worker-expert"), spanish.ItemId, 5);
        await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().AssignAsync(SkillSubject.User("later"), new SkillAssignmentInput
        {
            SkillId = spanish.ItemId,
            Proficiency = 5,
            EffectiveFrom = SkillsTestHarness.Today.AddDays(1),
        }, "tester"));

        // Act
        var holders = await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().FindHoldersAsync(spanish.ItemId, 3, SkillsTestHarness.Today, SkillSubjectKind.User), commit: false);
        var holds = await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().HoldsAsync(SkillSubject.User("novice"), spanish.ItemId, 3, SkillsTestHarness.Today), commit: false);

        // Assert
        Assert.Equal(["expert"], holders.Select(holder => holder.SubjectId));
        Assert.False(holds);
    }

    [Fact]
    public async Task RemoveAsync_TakesTheSkillAway_AndTellsTheHandlers()
    {
        // Arrange
        var handler = new RecordingAssignmentHandler();
        await using var harness = await SkillsTestHarness.CreateAsync(services => services.AddSingleton<ISkillAssignmentHandler>(handler));
        var spanish = await harness.AddSkillAsync("Spanish");

        await AssignAsync(harness, _ana, spanish.ItemId, 3);
        handler.Contexts.Clear();

        // Act
        var removed = await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().RemoveAsync(_ana, spanish.ItemId, "tester"));
        var again = await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().RemoveAsync(_ana, spanish.ItemId, "tester"));

        // Assert
        Assert.True(removed);
        Assert.False(again);
        var context = Assert.Single(handler.Contexts);
        Assert.Equal([spanish.ItemId], context.RemovedSkillIds);
        Assert.Empty(context.Assignments);
    }

    internal static Task<SkillsResult> SetAsync(SkillsTestHarness harness, SkillSubject subject, params (string SkillId, int Proficiency)[] skills)
    {
        return harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().SetAsync(
            subject,
            skills.Select(skill => new SkillAssignmentInput { SkillId = skill.SkillId, Proficiency = skill.Proficiency }),
            "tester"));
    }

    internal static Task<SkillsResult> AssignAsync(SkillsTestHarness harness, SkillSubject subject, string skillId, int proficiency)
    {
        return harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().AssignAsync(
            subject,
            new SkillAssignmentInput { SkillId = skillId, Proficiency = proficiency },
            "tester"));
    }

    internal static Task<CertificationRecord> SaveRecordAsync(SkillsTestHarness harness, SkillSubject subject, string certificationId, DateOnly issuedOn, CertificationStatus status, DateOnly? expiresOn = null)
    {
        return harness.RunAsync(async services =>
        {
            var record = new CertificationRecord
            {
                SubjectKind = subject.Kind,
                SubjectId = subject.Id,
                SubjectName = subject.Id,
                CertificationId = certificationId,
                IssuedOn = issuedOn,
                ExpiresOn = expiresOn,
                Status = status,
            };

            var result = await services.GetRequiredService<ICertificationRecordService>().SaveAsync(record, "tester");

            Assert.True(result.Succeeded, string.Join(" ", result.Errors));

            return record;
        });
    }

    private static async Task<string[]> EffectiveSkillIdsAsync(SkillsTestHarness harness, SkillSubject subject, DateOnly date)
    {
        var assignments = await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().GetEffectiveAsync(subject, date), commit: false);

        return assignments.Select(assignment => assignment.SkillId).ToArray();
    }

    internal sealed class RecordingAssignmentHandler : ISkillAssignmentHandler
    {
        public List<SkillAssignmentsChangedContext> Contexts { get; } = [];

        public Task AssignmentsChangedAsync(SkillAssignmentsChangedContext context, CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);

            return Task.CompletedTask;
        }
    }
}
