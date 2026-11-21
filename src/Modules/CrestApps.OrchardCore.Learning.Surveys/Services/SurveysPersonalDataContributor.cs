using System.Globalization;
using CrestApps.OrchardCore.Learning.Surveys.Core.Services;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Learning.Surveys.Services;

/// <summary>
/// Exports a person's survey answers, and deletes them when the person's data is anonymized: a survey answer is no
/// evidence of training, so nothing of it is kept.
/// </summary>
internal sealed class SurveysPersonalDataContributor : IPersonalDataContributor
{
    internal const string Kind = "LearningSurveyResponse";

    private readonly SurveyResponseStore _store;

    public SurveysPersonalDataContributor(SurveyResponseStore store)
    {
        _store = store;
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        foreach (var userId in context.Subject.UserIds)
        {
            foreach (var response in await _store.ListByUserAsync(userId, cancellationToken))
            {
                var record = context.Add(Kind, response.ItemId, response.SurveyTitle ?? response.SurveyId, response.SubmittedUtc);

                record.Fields["Survey"] = response.SurveyId;
                record.Fields["Course"] = response.CourseId;
                record.Fields["UpdatedUtc"] = response.UpdatedUtc?.ToString("O", CultureInfo.InvariantCulture);

                foreach (var (questionId, values) in response.Answers)
                {
                    record.Fields["Answer " + questionId] = string.Join(", ", values ?? []);
                }
            }
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        foreach (var userId in context.Subject.UserIds)
        {
            foreach (var response in await _store.ListByUserAsync(userId, cancellationToken))
            {
                await _store.DeleteAsync(response, cancellationToken);
                context.Result.Count(Kind);
            }
        }
    }
}
