using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.PersonalData;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

public sealed class AssessmentsPersonalDataContributorTests
{
    [Fact]
    public async Task TheExport_CarriesEveryAnswerEssaysIncluded_AndAnonymizingDeletesAttemptsThatProveNoCompletion()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.Questions.AddEssay("e1");
        h.AddQuiz("course1", "quiz", quiz => quiz.QuestionIds = ["e1"]);
        await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;
        await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 0, new AttemptResponse { Texts = ["I would leave by stairwell B."] }));

        // Act
        var export = new PersonalDataExport();
        var result = new PersonalDataAnonymizeResult();
        await h.RunAsync(async services =>
        {
            var contributor = ActivatorUtilities.CreateInstance<AssessmentsPersonalDataContributor>(services);
            var subject = new PersonalDataSubject(userId: "learner", email: null);

            await contributor.ExportAsync(new PersonalDataExportContext(subject, export));
            await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, null, result));

            return true;
        });

        // Assert
        Assert.Contains(export.Records, record => record.Kind == "LearningQuizAttempt" && record.Id == attempt.ItemId);
        Assert.Contains(export.Records, record => record.Kind == "LearningQuizAnswer" && record.Fields["Text"] == "I would leave by stairwell B.");
        Assert.Equal(1, result.Records["LearningQuizAttempt"]);
        Assert.Empty(await h.RunAsync(async services => await services.GetRequiredService<AssessmentAttemptStore>().ListByUserAsync("learner")));
    }
}
