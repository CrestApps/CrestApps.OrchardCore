using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The quiz editor's pickers: the fixed questions are searched on the server instead of listing every question, and
/// only people who edit quizzes or see question banks may search.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class QuizEditorTests
{
    private readonly LearningSiteFixture _fixture;

    public QuizEditorTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TheQuestionSearch_FindsPublishedQuestionsByTitle_AndIsRefusedToLearners()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (_, questionIds) = await CreateBankAsync(suffix, ("Ladder safety " + suffix, 2, ["ladders"]), ("Unrelated " + suffix, 3, []));

        using var admin = await _fixture.CreateClientAsync("admin");
        using var learner = await _fixture.CreateClientAsync("learner1");

        // Act
        var found = await admin.GetAsync($"Admin/learning/questions/search?query=Ladder%20safety%20{suffix}", TestContext.Current.CancellationToken);
        var refused = await learner.GetAsync($"Admin/learning/questions/search?query={suffix}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);

        using var json = JsonDocument.Parse(await found.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var item = Assert.Single(json.RootElement.EnumerateArray());

        Assert.Equal(questionIds[0], item.GetProperty("value").GetString());
        Assert.Equal("Ladder safety " + suffix, item.GetProperty("text").GetString());
        Assert.NotEqual(HttpStatusCode.OK, refused.StatusCode);
    }

    // A published bank of this test only, with published short-text questions of the given titles, difficulties and tags.
    internal Task<(string BankId, string[] QuestionIds)> CreateBankAsync(string suffix, params (string Title, int Difficulty, string[] Tags)[] questions)
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var bank = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.QuestionBank);

            bank.DisplayText = "Bank " + suffix;
            bank.Alter<TitlePart>(part => part.Title = "Bank " + suffix);
            await contentManager.CreateAsync(bank, VersionOptions.Published);

            var ids = new List<string>();

            foreach (var (title, difficulty, tags) in questions)
            {
                var question = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.ShortTextQuestion);

                question.DisplayText = title;
                question.Alter<TitlePart>(part => part.Title = title);
                question.Alter<QuestionPart>(part =>
                {
                    part.Prompt = "<p>" + title + "?</p>";
                    part.Points = 1;
                    part.Difficulty = difficulty;
                    part.Tags = [.. tags];
                });
                question.Alter<ShortTextQuestionPart>(part => part.AcceptedAnswers = ["yes"]);
                question.Alter<ContainedPart>(part =>
                {
                    part.ListContentItemId = bank.ContentItemId;
                    part.ListContentType = bank.ContentType;
                });
                await contentManager.CreateAsync(question, VersionOptions.Published);
                ids.Add(question.ContentItemId);
            }

            return (bank.ContentItemId, ids.ToArray());
        });
}
