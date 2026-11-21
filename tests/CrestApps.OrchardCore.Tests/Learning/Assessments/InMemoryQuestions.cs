using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// Questions and banks held in memory. Quizzes come from the in-memory courses.
/// </summary>
internal sealed class InMemoryQuestions : IAssessmentContentSource
{
    private readonly InMemoryCourses _courses;
    private readonly Dictionary<string, ContentItem> _questions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _banks = new(StringComparer.Ordinal);

    public InMemoryQuestions(InMemoryCourses courses)
    {
        _courses = courses;
    }

    public ContentItem AddChoice(string id, string correct, string bankId = null, string tag = null, decimal points = 1, params string[] options)
    {
        var question = New(id, AssessmentsConstants.ContentTypes.SingleChoiceQuestion, bankId, tag, points);

        question.Alter<ChoiceQuestionPart>(part => part.Options = (options.Length == 0 ? ["a", "b", "c"] : options)
            .Select(option => new ChoiceOption { Id = option, Text = "Option " + option, Correct = option == correct })
            .ToList());

        return question;
    }

    public ContentItem AddEssay(string id, decimal points = 5)
        => New(id, AssessmentsConstants.ContentTypes.EssayQuestion, null, null, points);

    public ContentItem AddQuestion(string id, string contentType, decimal points = 1, Action<ContentItem> configure = null)
    {
        var question = New(id, contentType, null, null, points);
        configure?.Invoke(question);

        return question;
    }

    public void Remove(string id)
        => _questions.Remove(id);

    public Task<ContentItem> GetItemAsync(string contentItemId)
    {
        if (_questions.TryGetValue(contentItemId, out var question))
        {
            return Task.FromResult(question);
        }

        try
        {
            return Task.FromResult(_courses.Get(contentItemId));
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult<ContentItem>(null);
        }
    }

    public Task<IReadOnlyList<ContentItem>> GetQuestionsAsync(IEnumerable<string> questionIds)
        => Task.FromResult<IReadOnlyList<ContentItem>>(questionIds.Where(_questions.ContainsKey).Select(id => _questions[id]).ToList());

    public Task<IReadOnlyList<ContentItem>> GetBankQuestionsAsync(string bankId)
        => Task.FromResult<IReadOnlyList<ContentItem>>(_banks.TryGetValue(bankId, out var ids) ? ids.Where(_questions.ContainsKey).Select(id => _questions[id]).ToList() : []);

    private ContentItem New(string id, string contentType, string bankId, string tag, decimal points)
    {
        var question = new ContentItem
        {
            ContentItemId = id,
            ContentItemVersionId = id + "-v1",
            ContentType = contentType,
            DisplayText = "Question " + id,
            Published = true,
            Latest = true,
        };

        question.Alter<QuestionPart>(part =>
        {
            part.Points = points;
            part.Prompt = "Prompt of " + id;
            part.Tags = tag is null ? [] : [tag];
        });

        _questions[id] = question;

        if (bankId is not null)
        {
            if (!_banks.TryGetValue(bankId, out var ids))
            {
                _banks[bankId] = ids = [];
            }

            ids.Add(id);
        }

        return question;
    }
}
