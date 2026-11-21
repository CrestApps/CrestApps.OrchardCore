using System.Text.Json.Nodes;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Certifications;
using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Recipes;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Certifications;

/// <summary>
/// The parts of the Learning Certifications module around the core: which certificates rest on an enrollment under
/// review, the recipe step, and the personal-data contributor.
/// </summary>
public sealed class CertificationModuleTests
{
    [Fact]
    public async Task ListRestingOn_FindsTheCertificateOfTheCourseEnrollment_AndOfTheEnrollmentTheExamWasTakenIn()
    {
        // Arrange
        await using var h = await CertificationHarness.CreateAsync();
        var path = await h.AddPathAsync();

        await h.CompleteCourseAsync("ana", "safety");
        await h.PassQuizAsync("ana");

        var progress = await h.ProgressAsync(path.ItemId, "ana");
        var course = (await h.EnrollmentsAsync("ana", "safety")).Single();
        var exam = (await h.EnrollmentsAsync("ana", "exams")).Single();
        h.Inner.Courses.AddCourse("other");
        h.Inner.Courses.AddItem("other", "other-lesson");

        var other = await h.Inner.EnrollAsync("ana", "other");

        // Act
        async Task<IReadOnlyList<(CertificationPathProgress Progress, string RecordId)>> RestingOn(CrestApps.OrchardCore.Learning.Models.Enrollment enrollment)
            => await h.RunAsync(services =>
            {
                var responses = services.GetRequiredService<ISurveyResponseQueries>();

                return services.GetRequiredService<DefaultCertificationPathService>().ListRestingOnAsync(enrollment, async id => LearningSurveyContexts.EnrollmentIdOf(await responses.FindByIdAsync(id)));
            });

        var fromCourse = await RestingOn(course);
        var fromExam = await RestingOn(exam);
        var fromOther = await RestingOn(other);

        // Assert
        Assert.Equal(progress.CurrentRecordId, Assert.Single(fromCourse).RecordId);
        Assert.Equal(progress.CurrentRecordId, Assert.Single(fromExam).RecordId);
        Assert.Empty(fromOther);
    }

    [Fact]
    public async Task RecipeStep_CreatesAPathByCertificationName_AndImportingAgainUpdatesTheSamePath()
    {
        // Arrange
        await using var h = await CertificationHarness.CreateAsync(configure: services => services.AddScoped<LearningCertificationPathsRecipeStep>());
        var existing = await h.AddPathAsync();
        var certificationName = "Workplace Safety";

        JsonObject Step(int window) => new()
        {
            ["name"] = CertificationsConstants.RecipeStepName,
            ["Paths"] = new JsonArray(new JsonObject
            {
                ["certificationName"] = certificationName,
                ["enabled"] = true,
                ["initialRequirements"] = new JsonArray(new JsonObject { ["kind"] = CertificationsConstants.RequirementKinds.CourseCompleted, ["targetId"] = "safety" }),
                ["renewalWindowDays"] = window,
            }),
        };

        // Act
        var first = await ImportAsync(h, Step(30));
        var second = await ImportAsync(h, Step(45));
        var paths = await h.RunAsync(services => services.GetRequiredService<ICertificationPathService>().ListAsync());

        // Assert
        Assert.Empty(first);
        Assert.Empty(second);

        var path = Assert.Single(paths);

        Assert.Equal(existing.ItemId, path.ItemId);
        Assert.Equal(45, path.RenewalWindowDays);
        Assert.Single(path.InitialRequirements);
    }

    [Fact]
    public async Task PersonalData_ExportsTheProgressWithItsCycles_AndAnonymizingDeletesIt()
    {
        // Arrange
        var evidence = new Mock<IProtectedFileStore<ExternalLearningEvidence>>();

        await using var h = await CertificationHarness.CreateAsync(configure: services =>
        {
            services.AddScoped<CertificationsPersonalDataContributor>();
            services.AddSingleton(evidence.Object);
        });
        var path = await h.AddPathAsync(join: RequirementJoin.Any);

        await h.CompleteCourseAsync("ana", "safety");

        var cpd = new EducationCreditType { Name = "CPD hours" };
        var submission = new ExternalLearningSubmission { UserId = "ana", Title = "Seminar", CompletedOn = CertificationHarness.Today, Amount = 2, EvidenceFileId = "file-1", EvidenceFileName = "seminar.pdf" };

        await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().SaveTypeAsync(cpd));
        submission.CreditTypeId = cpd.ItemId;
        await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().SubmitExternalAsync(submission));
        await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().AcceptExternalAsync(submission.ItemId, "reviewer-id", "reviewer", null));

        // Act
        var export = await h.RunAsync(async services =>
        {
            var context = new PersonalDataExportContext(new PersonalDataSubject("ana"), new PersonalDataExport());

            await services.GetRequiredService<CertificationsPersonalDataContributor>().ExportAsync(context, TestContext.Current.CancellationToken);

            return context.Export;
        });

        await h.RunAsync(async services =>
        {
            var context = new PersonalDataAnonymizeContext(new PersonalDataSubject("ana"), null, new PersonalDataAnonymizeResult());

            await services.GetRequiredService<CertificationsPersonalDataContributor>().AnonymizeAsync(context, TestContext.Current.CancellationToken);

            return context.Result;
        });

        var after = await h.ProgressAsync(path.ItemId, "ana");
        var creditsAfter = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().ListEntriesAsync("ana"));
        var externalAfter = await h.RunAsync(services => services.GetRequiredService<IEducationCreditService>().ListExternalForUserAsync("ana"));

        // Assert
        var record = Assert.Single(export.Records, item => item.Kind == "LearningCertificationProgress");

        Assert.Equal("2", Assert.Single(export.Records, item => item.Kind == "LearningEducationCredit").Fields["Amount"]);
        Assert.Equal("seminar.pdf", Assert.Single(export.Records, item => item.Kind == "LearningExternalLearning").Fields["Evidence"]);
        Assert.Empty(creditsAfter);
        Assert.Empty(externalAfter);
        evidence.Verify(store => store.DeleteAsync("file-1", It.IsAny<CancellationToken>()), Times.Once());

        Assert.Equal("Certified", record.Fields["State"]);
        Assert.Contains("1:", record.Fields["Cycles"]);
        Assert.Null(after);
        Assert.Single(await h.RecordsAsync("ana"));
    }

    private static Task<IList<string>> ImportAsync(CertificationHarness h, JsonObject step)
        => h.RunAsync(async services =>
        {
            var context = new RecipeExecutionContext { Name = step["name"]!.GetValue<string>(), Step = step };

            await services.GetRequiredService<LearningCertificationPathsRecipeStep>().ExecuteAsync(context);

            return context.Errors;
        });
}
