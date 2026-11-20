using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Competencies on a running site (Moodle migration item 14): the course editor's Competencies card, the learner's My
/// competencies page, the import of a Moodle competency framework, and the Skill holders report.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class CompetenciesPagesTests
{
    private readonly LearningSiteFixture _fixture;

    public CompetenciesPagesTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TheCourseEditor_HasACompetenciesCard_ListingSkillsByFramework()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("n")[..6];
        var skill = await AddSkillAsync("Hazard spotting " + suffix, "Site safety " + suffix);
        using var admin = await _fixture.CreateClientAsync("admin");

        // Act
        var onCourse = await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<IContentDefinitionManager>().GetTypeDefinitionAsync(LearningConstants.ContentTypes.Course)).Parts.Any(part => part.Name == nameof(CompetenciesPart)));
        var editor = await admin.GetStringAsync($"Admin/Contents/ContentItems/{_fixture.CourseId}/Edit", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(onCourse);
        Assert.Contains("Competencies granted on completion", editor);
        Assert.Contains($"label=\"Site safety {suffix}\"", editor);
        Assert.Contains(skill.ItemId, editor);
        Assert.Contains("selectpicker", editor);
    }

    [Fact]
    public async Task MyCompetencies_ListsTheLearnersSkillsByFramework_InsideTheAccountFrame()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("n")[..6];
        var skill = await AddSkillAsync("Lockout tagout " + suffix, "Machine safety " + suffix);

        await _fixture.InTenantAsync(services => services.GetRequiredService<ISkillAssignmentService>()
            .AssignAsync(SkillSubject.User(_fixture.SpareLearnerId), new SkillAssignmentInput { SkillId = skill.ItemId, Proficiency = 4 }, "Learning: Machine safety"));

        using var learner = await _fixture.CreateClientAsync("learner3");
        using var anonymous = await _fixture.CreateClientAsync();

        // Act
        var page = await learner.GetAsync("learning/competencies", TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var refused = await anonymous.GetAsync("learning/competencies", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Lockout tagout " + suffix, html);
        Assert.Contains("Machine safety " + suffix, html);
        Assert.Contains("4 - Advanced", html);
        Assert.Contains("class=\"commerce-account-nav\"", html);
        Assert.NotEqual(HttpStatusCode.OK, refused.StatusCode);
    }

    [Fact]
    public async Task ImportingAMoodleFrameworkExport_AddsItsCompetenciesAsSkills_InTheFrameworkCategory()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("n")[..6];
        var csv = new StringBuilder()
            .AppendLine("\"parentidnumber\",\"idnumber\",\"shortname\",\"description\",\"descriptionformat\",\"scalevalues\",\"scaleconfiguration\",\"ruletype\",\"ruleoutcome\",\"ruleconfig\",\"relatedidnumbers\",\"exportid\",\"isframework\",\"taxonomy\"")
            .AppendLine($"\"\",\"FW-{suffix}\",\"Leadership {suffix}\",\"\",\"1\",\"\",\"\",\"\",\"0\",\"\",\"\",\"1\",\"1\",\"\"")
            .AppendLine($"\"\",\"L1-{suffix}\",\"Delegation {suffix}\",\"<p>Hands work to the <b>right</b> person</p>\",\"1\",\"\",\"\",\"\",\"0\",\"\",\"\",\"2\",\"\",\"\"")
            .AppendLine($"\"L1-{suffix}\",\"L2-{suffix}\",\"Coaching {suffix}\",\"\",\"1\",\"\",\"\",\"\",\"0\",\"\",\"\",\"3\",\"\",\"\"")
            .ToString();
        using var admin = await _fixture.CreateClientAsync("admin");
        var form = await admin.GetStringAsync("Admin/skills/import", TestContext.Current.CancellationToken);

        // Act
        using var upload = new MultipartFormDataContent
        {
            { new StringContent(LearningSiteFixture.Token(form)), "__RequestVerificationToken" },
        };
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        upload.Add(file, "file", "framework.csv");

        var response = await admin.PostAsync("Admin/skills/import", upload, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var (delegation, coaching) = await _fixture.InTenantAsync(async services =>
        {
            var manager = services.GetRequiredService<ISkillManager>();

            return (await manager.FindByNameAsync("Delegation " + suffix), await manager.FindByNameAsync("Coaching " + suffix));
        });

        // Assert
        Assert.Contains("action-bar-bottom", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("2 skills added", html);
        Assert.Equal("Leadership " + suffix, delegation?.Category);
        Assert.Equal("Hands work to the right person", delegation.Description);
        Assert.Equal("Leadership " + suffix, coaching?.Category);
    }

    [Fact]
    public async Task TheSkillHoldersReport_ListsWhoHoldsEachCompetency_ForOneFramework()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("n")[..6];
        var skill = await AddSkillAsync("Forklift driving " + suffix, "Warehouse " + suffix);

        await AddSkillAsync("Other skill " + suffix, "Elsewhere " + suffix);
        await _fixture.InTenantAsync(services => services.GetRequiredService<ISkillAssignmentService>()
            .AssignAsync(SkillSubject.User(_fixture.EnrolledUserId), new SkillAssignmentInput { SkillId = skill.ItemId, Proficiency = 2 }, "Learning: Forklift basics"));

        using var admin = await _fixture.CreateClientAsync("admin");

        // Act
        var report = await admin.GetAsync("Admin/Reports/view/skills-skill-holders", TestContext.Current.CancellationToken);
        var html = await report.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        Assert.Contains("Forklift driving " + suffix, html);
        Assert.Contains("learner1", html);
        Assert.Contains("Learning: Forklift basics", html);
        Assert.Contains($"<option value=\"Warehouse {suffix}\"", html);
    }

    private Task<Skill> AddSkillAsync(string name, string category)
        => _fixture.InTenantAsync(async services =>
        {
            var manager = services.GetRequiredService<ISkillManager>();
            var skill = await manager.NewAsync();

            skill.Name = name;
            skill.Category = category;
            await manager.CreateAsync(skill);

            return skill;
        });
}
