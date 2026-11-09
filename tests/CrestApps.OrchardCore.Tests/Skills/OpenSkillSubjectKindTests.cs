using System.Text.Json;
using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Skills.Core.Indexes;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Skills;

/// <summary>
/// The kind of record that holds a skill is an open string, so a module adds its own kind of person without changing
/// Skills, and records saved while the kind was a number are still read.
/// </summary>
public sealed class OpenSkillSubjectKindTests
{
    [Fact]
    public async Task AModuleOwnSubjectKind_HoldsSkills_AndIsFoundByThatKindOnly()
    {
        // Arrange
        await using var harness = await SkillsTestHarness.CreateAsync();
        var spanish = await harness.AddSkillAsync("Spanish");
        var agent = new SkillSubject("ExternalAgent", "agent-1");

        await SkillsTests.AssignAsync(harness, agent, spanish.ItemId, 4);
        await SkillsTests.AssignAsync(harness, SkillSubject.User("agent-1"), spanish.ItemId, 4);

        // Act
        var holders = await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().FindHoldersAsync(spanish.ItemId, 3, SkillsTestHarness.Today, "ExternalAgent"), commit: false);
        var held = await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().GetAssignmentsAsync(agent), commit: false);

        // Assert
        var holder = Assert.Single(holders);
        Assert.Equal("ExternalAgent", holder.SubjectKind);
        Assert.Equal(agent, holder.Subject);
        Assert.Single(held);
    }

    [Theory]
    [InlineData("0", SkillSubjectKind.User)]
    [InlineData("1", SkillSubjectKind.Worker)]
    [InlineData("\"Worker\"", SkillSubjectKind.Worker)]
    [InlineData("\"ExternalAgent\"", "ExternalAgent")]
    public void ADocumentSavedWithANumberOrAName_IsRead(string storedKind, string expected)
    {
        // Act
        var assignment = JsonSerializer.Deserialize<SkillAssignment>($$"""{"SubjectKind":{{storedKind}},"SubjectId":"s1","SkillId":"k1","Proficiency":3}""");
        var record = JsonSerializer.Deserialize<CertificationRecord>($$"""{"SubjectKind":{{storedKind}},"SubjectId":"s1"}""");

        // Assert
        Assert.Equal(expected, assignment.SubjectKind);
        Assert.Equal(expected, record.SubjectKind);
        Assert.Contains($"\"SubjectKind\":\"{expected}\"", JsonSerializer.Serialize(assignment), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheUpgrade_FillsTheKindNameOfRecordsIndexedBeforeTheColumnExisted()
    {
        // Arrange
        await using var harness = await SkillsTestHarness.CreateAsync();
        var spanish = await harness.AddSkillAsync("Spanish");

        await SkillsTests.AssignAsync(harness, SkillSubject.User("u1"), spanish.ItemId, 3);
        await SkillsTests.AssignAsync(harness, SkillSubject.Worker("w1"), spanish.ItemId, 3);

        var store = harness.Store;
        var table = store.Configuration.TableNameConvention.GetIndexTable(typeof(SkillAssignmentIndex), SkillsConstants.CollectionName);

        await using (var connection = store.Configuration.ConnectionFactory.CreateConnection())
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);

            await using var command = connection.CreateCommand();

            // Rows as an upgraded database holds them right after the column is added: no name yet.
            command.CommandText = $"UPDATE {table} SET SubjectKindName = NULL";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        // Act
        await using (var session = store.CreateSession())
        {
            await CatalogIndexBackfill.ReindexAsync<SkillAssignment, SkillAssignmentIndex>(session, SkillsConstants.CollectionName);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Assert
        await using var reading = store.CreateSession();
        var rows = await reading.QueryIndex<SkillAssignmentIndex>(collection: SkillsConstants.CollectionName).ListAsync(TestContext.Current.CancellationToken);

        Assert.Contains(rows, row => row.SubjectId == "u1" && row.SubjectKindName == SkillSubjectKind.User);
        Assert.Contains(rows, row => row.SubjectId == "w1" && row.SubjectKindName == SkillSubjectKind.Worker);
    }
}
