using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Skills.Core.Indexes;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Skills;

/// <summary>
/// Until the deferred re-index fills the <c>SubjectKindName</c> column, an index row only has the number the kind was
/// stored as (0 for a user, 1 for a worker). The queries still find those rows, so older assignments and certifications
/// stay visible and assigning a skill again updates the assignment instead of adding a second one.
/// </summary>
public sealed class SkillsLegacyKindIndexTests
{
    [Fact]
    public async Task AnAssignmentIndexedWithOnlyTheNumber_IsStillFound_AndNotDuplicated()
    {
        // Arrange
        await using var harness = await SkillsTestHarness.CreateAsync();
        var spanish = await harness.AddSkillAsync("Spanish");

        await SkillsTests.AssignAsync(harness, SkillSubject.User("u1"), spanish.ItemId, 3);
        await SkillsTests.AssignAsync(harness, SkillSubject.Staff("w1"), spanish.ItemId, 4);
        await ClearKindNamesAsync(harness, typeof(SkillAssignmentIndex));

        // Act
        var userAssignments = await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().GetAssignmentsAsync(SkillSubject.User("u1")), commit: false);
        var workerHolders = await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().FindHoldersAsync(spanish.ItemId, 1, SkillsTestHarness.Today, SkillSubjectKind.Staff), commit: false);
        var userHolders = await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().FindHoldersAsync(spanish.ItemId, 1, SkillsTestHarness.Today, SkillSubjectKind.User), commit: false);

        await SkillsTests.AssignAsync(harness, SkillSubject.User("u1"), spanish.ItemId, 5);
        var afterReassign = await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().GetAssignmentsAsync(SkillSubject.User("u1")), commit: false);

        // Assert
        Assert.Single(userAssignments);
        Assert.Equal("w1", Assert.Single(workerHolders).SubjectId);
        Assert.Equal("u1", Assert.Single(userHolders).SubjectId);
        Assert.Equal(5, Assert.Single(afterReassign).Proficiency);
    }

    [Fact]
    public async Task ACertificationIndexedWithOnlyTheNumber_IsStillFound()
    {
        // Arrange
        await using var harness = await SkillsTestHarness.CreateAsync();
        var forklift = await harness.AddCertificationAsync("Forklift");
        var worker = SkillSubject.Staff("w1");

        await SkillsTests.SaveRecordAsync(harness, worker, forklift.ItemId, SkillsTestHarness.Today.AddDays(-10), CertificationStatus.Valid, SkillsTestHarness.Today.AddDays(300));
        await ClearKindNamesAsync(harness, typeof(CertificationRecordIndex));

        // Act
        var records = await harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().GetForSubjectAsync(worker), commit: false);
        var isValid = await harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().IsValidOnAsync(worker, forklift.ItemId, SkillsTestHarness.Today), commit: false);
        var page = await harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().PageAsync(new CertificationRecordQuery { Subject = worker }, 0, 10), commit: false);
        var asUser = await harness.RunAsync(services => services.GetRequiredService<ICertificationRecordService>().GetForSubjectAsync(SkillSubject.User("w1")), commit: false);

        // Assert
        Assert.Single(records);
        Assert.True(isValid);
        Assert.Equal(1, page.Count);
        Assert.Empty(asUser);
    }

    private static async Task ClearKindNamesAsync(SkillsTestHarness harness, Type indexType)
    {
        var store = harness.Store;
        var table = store.Configuration.TableNameConvention.GetIndexTable(indexType, SkillsConstants.CollectionName);

        await using var connection = store.Configuration.ConnectionFactory.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();

        // Rows as an upgraded database holds them until the deferred re-index runs: the number only.
        command.CommandText = $"UPDATE {table} SET SubjectKindName = NULL";
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
