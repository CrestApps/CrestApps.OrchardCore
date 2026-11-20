using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;
using CrestApps.OrchardCore.ContentTransfer.Indexes;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Entities;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Competencies on a running site (Moodle migration item 14): the course editor's Competencies card, the learner's My
/// competencies page, the import of a Moodle competency framework through ContentTransfer (with its failed rows and
/// its own permission), and the Skill holders report.
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
    public async Task ImportingAMoodleFrameworkExport_ThroughContentTransfer_AddsSkills_AndListsTheFailedRows()
    {
        // Arrange: a Moodle export with one competency that has neither a short name nor an ID number.
        var suffix = Guid.NewGuid().ToString("n")[..6];
        var csv = new StringBuilder()
            .AppendLine("\"parentidnumber\",\"idnumber\",\"shortname\",\"description\",\"descriptionformat\",\"scalevalues\",\"scaleconfiguration\",\"ruletype\",\"ruleoutcome\",\"ruleconfig\",\"relatedidnumbers\",\"exportid\",\"isframework\",\"taxonomy\"")
            .AppendLine($"\"\",\"FW-{suffix}\",\"Leadership {suffix}\",\"\",\"1\",\"\",\"\",\"\",\"0\",\"\",\"\",\"1\",\"1\",\"\"")
            .AppendLine($"\"\",\"L1-{suffix}\",\"Delegation {suffix}\",\"<p>Hands work to the <b>right</b> person</p>\",\"1\",\"\",\"\",\"\",\"0\",\"\",\"\",\"2\",\"\",\"\"")
            .AppendLine($"\"L1-{suffix}\",\"L2-{suffix}\",\"Coaching {suffix}\",\"\",\"1\",\"\",\"\",\"\",\"0\",\"\",\"\",\"3\",\"\",\"\"")
            .AppendLine("\"\",\"\",\"\",\"No name at all\",\"1\",\"\",\"\",\"\",\"0\",\"\",\"\",\"4\",\"\",\"\"")
            .ToString();
        using var admin = await _fixture.CreateClientAsync("admin");
        var skillsList = await admin.GetStringAsync("Admin/skills", TestContext.Current.CancellationToken);
        var form = await admin.GetStringAsync("Admin/import/contents/Skills.Skill", TestContext.Current.CancellationToken);
        var template = await admin.GetStringAsync("Admin/import/contents/Skills.Skill/download-template?format=.csv", TestContext.Current.CancellationToken);

        // Act
        using var upload = new MultipartFormDataContent
        {
            { new StringContent(LearningSiteFixture.Token(form)), "__RequestVerificationToken" },
        };
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        upload.Add(file, FileFieldName(form), $"framework-{suffix}.csv");

        var queued = await admin.PostAsync("Admin/import/contents/Skills.Skill", upload, TestContext.Current.CancellationToken);
        var queuedBody = await queued.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(queued.StatusCode == HttpStatusCode.OK, queuedBody);

        var entry = await RunImportAsync($"framework-{suffix}.csv");
        var errors = await admin.GetStringAsync($"Admin/import/entries/{entry.EntryId}/download-errors", TestContext.Current.CancellationToken);
        var (delegation, coaching) = await _fixture.InTenantAsync(async services =>
        {
            var manager = services.GetRequiredService<ISkillManager>();

            return (await manager.FindByNameAsync("Delegation " + suffix), await manager.FindByNameAsync("Coaching " + suffix));
        });

        // Assert: the Skills list links to the ContentTransfer import, which has no content options.
        Assert.Contains("import/contents/Skills.Skill", skillsList);
        Assert.Contains("Import Skills and competency frameworks", form);
        Assert.DoesNotContain("Publish imported content", form);
        Assert.StartsWith("Name,Category,Description,Enabled", template, StringComparison.Ordinal);
        Assert.Contains("success", queuedBody);
        Assert.Equal(SkillsTargetName, entry.ContentType);
        Assert.Equal(ContentTransferEntryStatus.CompletedWithErrors, entry.Status);
        Assert.Equal(3, entry.As<ImportFileProcessStatsPart>().ImportedCount);
        Assert.Equal([4], entry.As<ImportFileProcessStatsPart>().Errors);
        Assert.Contains("No name at all", errors);
        Assert.Contains("neither a short name nor an ID number", errors);
        Assert.Equal("Leadership " + suffix, delegation?.Category);
        Assert.Equal("Hands work to the right person", delegation.Description);
        Assert.Equal("Leadership " + suffix, coaching?.Category);
    }

    [Fact]
    public async Task TheSkillsImport_KeepsItsOwnPermission_EvenForSomeoneWhoMayImportEveryContentType()
    {
        // Arrange: a role that may import every content type, but not manage skills.
        var userName = "importer" + Guid.NewGuid().ToString("n")[..6];
        await _fixture.InTenantAsync(async services =>
        {
            var roles = services.GetRequiredService<RoleManager<IRole>>();

            if (await roles.FindByNameAsync("ContentImporter") is null)
            {
                await roles.CreateAsync(new Role
                {
                    RoleName = "ContentImporter",
                    RoleClaims =
                    [
                        new RoleClaim { ClaimType = Permission.ClaimType, ClaimValue = "AccessAdminPanel" },
                        new RoleClaim { ClaimType = Permission.ClaimType, ClaimValue = ContentTransferPermissions.ImportContentFromFile.Name },
                    ],
                });
            }

            await LearningSiteFixture.CreateUserAsync(services, userName, "ContentImporter");
        });
        using var importer = await _fixture.CreateClientAsync(userName);
        using var admin = await _fixture.CreateClientAsync("admin");

        // Act
        var importPage = await importer.GetAsync("Admin/import/contents/Skills.Skill", TestContext.Current.CancellationToken);
        var template = await importer.GetAsync("Admin/import/contents/Skills.Skill/download-template?format=.csv", TestContext.Current.CancellationToken);
        var contentImport = await importer.GetAsync($"Admin/import/contents/{LearningConstants.ContentTypes.Course}", TestContext.Current.CancellationToken);
        var adminPage = await admin.GetAsync("Admin/import/contents/Skills.Skill", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, importPage.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, template.StatusCode);
        Assert.Equal(HttpStatusCode.OK, contentImport.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminPage.StatusCode);
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

    private const string SkillsTargetName = "Skills.Skill";

    // The name the import form gives its file input, as a browser posts it.
    private static string FileFieldName(string form)
        => System.Text.RegularExpressions.Regex.Match(form, "<input[^>]*type=\"file\"[^>]*name=\"([^\"]+)\"").Groups[1].Value is { Length: > 0 } name
            ? name
            : System.Text.RegularExpressions.Regex.Match(form, "<input[^>]*name=\"([^\"]+)\"[^>]*type=\"file\"").Groups[1].Value;

    private async Task<ContentTransferEntry> RunImportAsync(string uploadedFileName)
    {
        var entryId = await _fixture.InTenantAsync(async services => (await services.GetRequiredService<ISession>()
            .Query<ContentTransferEntry, ContentTransferEntryIndex>(index => index.ContentType == SkillsTargetName)
            .ListAsync())
            .Single(entry => entry.UploadedFileName == uploadedFileName)
            .EntryId);

        // The import runs in a scope of its own, as the deferred task the upload starts runs it.
        await _fixture.InTenantAsync(services =>
        {
            var method = typeof(ImportFilesBackgroundTask).GetMethod("ProcessEntriesAsync", BindingFlags.NonPublic | BindingFlags.Static, [typeof(IServiceProvider), typeof(string), typeof(CancellationToken)]);

            return (Task)method.Invoke(null, [services, entryId, CancellationToken.None]);
        });

        return await _fixture.InTenantAsync(services => services.GetRequiredService<ISession>()
            .Query<ContentTransferEntry, ContentTransferEntryIndex>(index => index.EntryId == entryId)
            .FirstOrDefaultAsync());
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
