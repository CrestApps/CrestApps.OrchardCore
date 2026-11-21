using System.Security.Claims;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Skills.ContentTransfer;
using CrestApps.OrchardCore.Skills.Core.Services;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Skills;

/// <summary>
/// Importing skills and competency frameworks (Moodle migration item 14) through ContentTransfer: a Moodle competency
/// framework export, where the framework becomes the skill category, or a plain file with Name, Category, Description
/// and Enabled columns.
/// </summary>
public sealed class SkillImportTests
{
    private static readonly string[] _moodleColumns = ["parentidnumber", "idnumber", "shortname", "description", "descriptionformat", "isframework"];

    [Fact]
    public void TheRowReader_ReadsAMoodleExport_IntoSkillsOfTheFrameworkCategory()
    {
        // Arrange
        var reader = new SkillImportRowReader();

        // Act
        var framework = reader.Read(1, Moodle("", "CS-FW", "Customer Service", "<p>Serving customers</p>", "1", "1"));
        var communication = reader.Read(2, Moodle("", "CS-1", "Communication", "<p>Speaks <b>clearly</b> &amp; listens.</p>", "1", ""));
        var listening = reader.Read(3, Moodle("CS-1", "CS-1-1", "Active listening", "Repeats back what was asked", "2", ""));
        var noShortName = reader.Read(4, Moodle("", "CS-2", "", "No short name", "0", ""));

        // Assert
        Assert.Equal("Customer Service", framework.Framework);
        Assert.Null(framework.Skill);
        Assert.Equal(("Communication", "Customer Service", "Speaks clearly & listens."), (communication.Skill.Name, communication.Skill.Category, communication.Skill.Description));
        Assert.Equal(("Active listening", "Customer Service", "Repeats back what was asked"), (listening.Skill.Name, listening.Skill.Category, listening.Skill.Description));
        Assert.Equal("CS-2", noShortName.Skill.Name);
    }

    [Fact]
    public void TheRowReader_ReadsAPlainFile_InAnyCase_AndSaysWhyARowCannotBeRead()
    {
        // Arrange
        var reader = new SkillImportRowReader();

        // Act
        var spanish = reader.Read(2, Cells(("name", "Spanish"), ("CATEGORY", "Languages"), ("Description", "Speaks Spanish"), ("Enabled", "yes")));
        var welding = reader.Read(3, Cells(("Name", "Welding"), ("Enabled", "false")));
        var noName = reader.Read(4, Cells(("Name", ""), ("Category", "Languages")));
        var badEnabled = reader.Read(5, Cells(("Name", "Driving"), ("Enabled", "maybe")));
        var beforeFramework = new SkillImportRowReader().Read(2, Moodle("", "X", "Orphan", "", "1", ""));

        // Assert
        Assert.Equal(("Spanish", "Languages", "Speaks Spanish", (bool?)true), (spanish.Skill.Name, spanish.Skill.Category, spanish.Skill.Description, spanish.Skill.Enabled));
        Assert.Equal(("Welding", null, (bool?)false), (welding.Skill.Name, welding.Skill.Category, welding.Skill.Enabled));
        Assert.Contains("no name", noName.Error, StringComparison.Ordinal);
        Assert.Contains("maybe", badEnabled.Error, StringComparison.Ordinal);
        Assert.Contains("before the framework row", beforeFramework.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheImporter_CreatesNewSkills_UpdatesTheSameSkill_AndFailsANameAnotherFrameworkHolds()
    {
        // Arrange: Communication exists in the same framework, Teamwork in another and Planning with no category.
        await using var h = await SkillsTestHarness.CreateAsync();

        await h.AddSkillAsync("Communication", skill => skill.Category = "Customer Service");
        await h.AddSkillAsync("Teamwork", skill => skill.Category = "Leadership");
        await h.AddSkillAsync("planning");

        var rows = new[]
        {
            new SkillImportRow { Line = 2, Name = "Communication", Category = "Customer Service", Description = "Speaks clearly" },
            new SkillImportRow { Line = 3, Name = "Teamwork", Category = "Customer Service" },
            new SkillImportRow { Line = 4, Name = "Planning", Category = "Customer Service" },
            new SkillImportRow { Line = 5, Name = "Empathy", Category = "Customer Service", Description = "Feels with the caller", Enabled = false },
            new SkillImportRow { Line = 6, Name = "empathy", Category = "Customer Service" },
        };

        // Act
        var result = await h.RunAsync(services => services.GetRequiredService<SkillImporter>().ImportAsync(rows, TestContext.Current.CancellationToken));
        var again = await h.RunAsync(services => services.GetRequiredService<SkillImporter>().ImportAsync(rows.Take(1), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(1, result.Created);
        Assert.Equal(2, result.Updated);
        Assert.Equal([3, 6], result.Failures.Keys.Order());
        Assert.Contains("Leadership", result.Failures[3], StringComparison.Ordinal);
        Assert.Equal(1, again.Unchanged);

        var skills = (await h.RunAsync(async services => await services.GetRequiredService<ISkillManager>().GetAllAsync())).ToList();
        var byName = skills.ToDictionary(skill => skill.Name, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(4, skills.Count);
        Assert.Equal("Speaks clearly", byName["Communication"].Description);
        Assert.Equal("Leadership", byName["Teamwork"].Category);
        Assert.Equal("Customer Service", byName["Planning"].Category);
        Assert.False(byName["Empathy"].Enabled);
    }

    [Fact]
    public async Task TheContentTransferTarget_ImportsAMoodleExport_AcrossBatches_KeepingTheFramework()
    {
        // Arrange: the framework row is in the first batch and its competencies in the next, as after a resume.
        await using var h = await SkillsTestHarness.CreateAsync(services => services.AddScoped<SkillsImportTarget>());
        var entry = new ContentTransferEntry { EntryId = "e1", ContentType = SkillsImportTarget.TargetName };
        var first = Batch(entry, MoodleRow(1, "", "CS-FW", "Customer Service", "", "1", "1"), MoodleRow(2, "", "CS-1", "Communication", "<p>Speaks clearly</p>", "1", ""));
        var second = Batch(entry, MoodleRow(3, "CS-1", "CS-1-1", "Active listening", "", "1", ""));

        // Act
        await h.RunAsync(services => services.GetRequiredService<SkillsImportTarget>().ImportAsync(first, TestContext.Current.CancellationToken));
        await h.RunAsync(services => services.GetRequiredService<SkillsImportTarget>().ImportAsync(second, TestContext.Current.CancellationToken));

        // Assert
        Assert.DoesNotContain(first.Rows.Concat(second.Rows), row => row.Failed);

        var skills = (await h.RunAsync(async services => await services.GetRequiredService<ISkillManager>().GetAllAsync())).ToDictionary(skill => skill.Name);

        Assert.Equal(["Active listening", "Communication"], skills.Keys.Order());
        Assert.All(skills.Values, skill => Assert.Equal("Customer Service", skill.Category));
        Assert.Equal("Speaks clearly", skills["Communication"].Description);
    }

    [Fact]
    public async Task TheContentTransferTarget_FailsTheRowsItCannotSave_WithTheirReasons()
    {
        // Arrange
        await using var h = await SkillsTestHarness.CreateAsync(services => services.AddScoped<SkillsImportTarget>());
        await h.AddSkillAsync("Teamwork", skill => skill.Category = "Leadership");

        var batch = Batch(
            new ContentTransferEntry { EntryId = "e2", ContentType = SkillsImportTarget.TargetName },
            PlainRow(1, "Spanish", "Languages"),
            PlainRow(2, "", "Languages"),
            PlainRow(3, "Teamwork", "Languages"));

        // Act
        await h.RunAsync(services => services.GetRequiredService<SkillsImportTarget>().ImportAsync(batch, TestContext.Current.CancellationToken));

        // Assert
        Assert.False(batch.Rows[0].Failed);
        Assert.Contains("no name", batch.Rows[1].Error, StringComparison.Ordinal);
        Assert.Contains("Leadership", batch.Rows[2].Error, StringComparison.Ordinal);
        Assert.NotNull(await h.RunAsync(services => services.GetRequiredService<ISkillManager>().FindByNameAsync("Spanish")));
    }

    [Fact]
    public async Task TheContentTransferTarget_NeedsManageSkills_AndDescribesItsColumns()
    {
        // Arrange
        await using var h = await SkillsTestHarness.CreateAsync(services => services.AddScoped<SkillsImportTarget>());
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ana")], "test"));
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements)
                => requirements.OfType<PermissionRequirement>().Any(requirement => requirement.Permission.Name == SkillsPermissions.ManageSkills.Name)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        // Act
        var (allowed, columns, name) = await h.RunAsync(async services =>
        {
            var target = services.GetRequiredService<SkillsImportTarget>();

            return (await target.AuthorizeAsync(user, authorization.Object), await target.GetColumnsAsync(TestContext.Current.CancellationToken), target.Name);
        });

        // Assert
        Assert.True(allowed);
        Assert.Contains('.', name);
        Assert.Equal(["Name", "Category", "Description", "Enabled"], columns.Where(column => column.Type != ImportColumnType.ExportOnly).Select(column => column.Name).Take(4));
        Assert.True(columns.Single(column => column.Name == "Name").IsRequired);
        Assert.Contains("shortname", columns.Single(column => column.Name == "Name").AdditionalNames);
    }

    private static Dictionary<string, string> Cells(params (string Column, string Value)[] cells)
        => cells.ToDictionary(cell => cell.Column, cell => cell.Value, StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, string> Moodle(string parent, string idNumber, string shortName, string description, string format, string isFramework)
        => _moodleColumns.Zip([parent, idNumber, shortName, description, format, isFramework]).ToDictionary(pair => pair.First, pair => pair.Second, StringComparer.OrdinalIgnoreCase);

    private static ContentTransferImportRow MoodleRow(int index, string parent, string idNumber, string shortName, string description, string format, string isFramework)
        => new(index, Moodle(parent, idNumber, shortName, description, format, isFramework));

    private static ContentTransferImportRow PlainRow(int index, string name, string category)
        => new(index, Cells(("Name", name), ("Category", category)));

    private static ContentTransferImportBatch Batch(ContentTransferEntry entry, params ContentTransferImportRow[] rows)
        => new(entry, rows);
}
