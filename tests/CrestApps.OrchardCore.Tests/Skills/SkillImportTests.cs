using CrestApps.OrchardCore.Skills.Core.Services;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Skills;

/// <summary>
/// Importing skills from a CSV file (Moodle migration item 14): a Moodle competency framework export, where the
/// framework becomes the skill category, or a plain file with Name, Category and Description columns.
/// </summary>
public sealed class SkillImportTests
{
    private const string MoodleExport =
        "\"parentidnumber\",\"idnumber\",\"shortname\",\"description\",\"descriptionformat\",\"scalevalues\",\"scaleconfiguration\",\"ruletype\",\"ruleoutcome\",\"ruleconfig\",\"relatedidnumbers\",\"exportid\",\"isframework\",\"taxonomy\"\n" +
        "\"\",\"CS-FW\",\"Customer Service\",\"<p>Serving customers</p>\",\"1\",\"Not yet,Competent\",\"[]\",\"\",\"0\",\"\",\"\",\"1\",\"1\",\"behaviour,competency\"\n" +
        "\"\",\"CS-1\",\"Communication\",\"<p>Speaks <b>clearly</b> &amp; listens.</p>\",\"1\",\"\",\"\",\"\",\"0\",\"\",\"\",\"2\",\"\",\"\"\n" +
        "\"CS-1\",\"CS-1-1\",\"Active listening\",\"Repeats back what was asked\",\"0\",\"\",\"\",\"\",\"0\",\"\",\"\",\"3\",\"\",\"\"\n" +
        "\"\",\"CS-2\",\"\",\"No short name\",\"0\",\"\",\"\",\"\",\"0\",\"\",\"\",\"4\",\"\",\"\"\n";

    [Fact]
    public void Parse_AMoodleFrameworkExport_MakesEveryCompetencyASkillInTheFrameworkCategory()
    {
        // Act
        var result = SkillCsvImport.Parse(MoodleExport);

        // Assert
        Assert.Equal(SkillCsvImport.MoodleFormat, result.Format);
        Assert.Empty(result.Errors);
        Assert.Collection(result.Rows,
            row =>
            {
                Assert.Equal("Communication", row.Name);
                Assert.Equal("Customer Service", row.Category);
                Assert.Equal("Speaks clearly & listens.", row.Description);
                Assert.Equal(3, row.Line);
            },
            row =>
            {
                Assert.Equal("Active listening", row.Name);
                Assert.Equal("Customer Service", row.Category);
                Assert.Equal("Repeats back what was asked", row.Description);
            },
            row =>
            {
                Assert.Equal("CS-2", row.Name);
                Assert.Equal("Customer Service", row.Category);
            });
    }

    [Fact]
    public void Parse_APlainFile_ReadsNameCategoryDescriptionAndEnabled_InAnyCase()
    {
        // Act
        var result = SkillCsvImport.Parse("name,CATEGORY,Description,Enabled\nSpanish,Languages,Speaks Spanish,yes\nWelding,,,false\n,Languages,no name,\n");

        // Assert
        Assert.Equal(SkillCsvImport.PlainFormat, result.Format);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(("Spanish", "Languages", "Speaks Spanish", (bool?)true), (result.Rows[0].Name, result.Rows[0].Category, result.Rows[0].Description, result.Rows[0].Enabled));
        Assert.Equal(("Welding", null, null, (bool?)false), (result.Rows[1].Name, result.Rows[1].Category, result.Rows[1].Description, result.Rows[1].Enabled));
        Assert.Contains(result.Errors, error => error.Contains('4', StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_AFileWithoutANameColumn_IsRefused()
    {
        // Act
        var result = SkillCsvImport.Parse("Title,Group\nSpanish,Languages\n");

        // Assert
        Assert.Empty(result.Rows);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task Import_CreatesNewSkills_UpdatesTheSameSkill_AndSkipsANameAnotherFrameworkHolds()
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
        Assert.Equal(2, result.Skipped.Count);
        Assert.Contains(result.Skipped, message => message.Contains("Leadership", StringComparison.Ordinal));
        Assert.Equal(1, again.Unchanged);

        var skills = (await h.RunAsync(async services => await services.GetRequiredService<ISkillManager>().GetAllAsync())).ToList();
        var byName = skills.ToDictionary(skill => skill.Name, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(4, skills.Count);
        Assert.Equal("Speaks clearly", byName["Communication"].Description);
        Assert.Equal("Leadership", byName["Teamwork"].Category);
        Assert.Equal("Customer Service", byName["Planning"].Category);
        Assert.False(byName["Empathy"].Enabled);
        Assert.Equal("Feels with the caller", byName["Empathy"].Description);
    }
}
