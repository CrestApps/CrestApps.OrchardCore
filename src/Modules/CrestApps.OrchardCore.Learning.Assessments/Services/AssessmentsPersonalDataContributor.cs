using System.Globalization;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Learning.Assessments.Services;

/// <summary>
/// Exports a person's quiz attempts, with every answer they gave (essays included), and their quiz overrides; anonymizes
/// them by deleting attempts that are not evidence of a completed enrollment and every override.
/// </summary>
internal sealed class AssessmentsPersonalDataContributor : IPersonalDataContributor
{
    private const string AttemptKind = "LearningQuizAttempt";
    private const string AnswerKind = "LearningQuizAnswer";
    private const string OverrideKind = "LearningQuizOverride";

    private readonly AssessmentAttemptStore _attemptStore;
    private readonly AssessmentOverrideStore _overrideStore;
    private readonly EnrollmentStore _enrollmentStore;

    public AssessmentsPersonalDataContributor(AssessmentAttemptStore attemptStore, AssessmentOverrideStore overrideStore, EnrollmentStore enrollmentStore)
    {
        _attemptStore = attemptStore;
        _overrideStore = overrideStore;
        _enrollmentStore = enrollmentStore;
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        foreach (var userId in context.Subject.UserIds)
        {
            foreach (var attempt in await _attemptStore.ListByUserAsync(userId, cancellationToken))
            {
                var record = context.Add(AttemptKind, attempt.ItemId, attempt.QuizTitle ?? attempt.QuizId, attempt.StartedUtc);

                record.Fields["Number"] = attempt.Number;
                record.Fields["Quiz"] = attempt.QuizId;
                record.Fields["Status"] = attempt.Status.ToString();
                record.Fields["Score"] = attempt.Score?.ToString(CultureInfo.InvariantCulture);
                record.Fields["Percent"] = attempt.Percent?.ToString(CultureInfo.InvariantCulture);
                record.Fields["Submitted"] = attempt.SubmittedUtc?.ToString("O", CultureInfo.InvariantCulture);
                record.Fields["Feedback"] = attempt.Feedback;

                foreach (var item in attempt.Items.Where(item => item.Response is not null))
                {
                    var answer = context.Add(AnswerKind, attempt.ItemId + ":" + item.Index, item.Title ?? item.QuestionId, item.Response.AnsweredUtc);

                    answer.Fields["Choices"] = string.Join(" | ", item.Response.ChoiceIds.Select(id => item.Choices.FirstOrDefault(choice => choice.Id == id)?.Text ?? id));
                    answer.Fields["Text"] = string.Join(" | ", item.Response.Texts);
                    answer.Fields["Number"] = item.Response.Number?.ToString(CultureInfo.InvariantCulture);
                    answer.Fields["Score"] = item.Score?.ToString(CultureInfo.InvariantCulture);
                    answer.Fields["GraderComment"] = item.GraderComment;
                }
            }

            foreach (var entry in await _overrideStore.ListByUserAsync(userId, cancellationToken))
            {
                var record = context.Add(OverrideKind, entry.ItemId, entry.QuizId, entry.CreatedUtc);

                record.Fields["ExtraMinutes"] = entry.ExtraMinutes.ToString(CultureInfo.InvariantCulture);
                record.Fields["ExtraAttempts"] = entry.ExtraAttempts.ToString(CultureInfo.InvariantCulture);
                record.Fields["Reason"] = entry.Reason;
            }
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        foreach (var userId in context.Subject.UserIds)
        {
            var kept = 0;

            foreach (var attempt in await _attemptStore.ListByUserAsync(userId, cancellationToken))
            {
                var enrollment = string.IsNullOrEmpty(attempt.EnrollmentId) ? null : await _enrollmentStore.FindByIdAsync(attempt.EnrollmentId, cancellationToken);

                // An attempt that a completed enrollment rests on is evidence of the training, like the enrollment itself.
                if (enrollment?.Status == EnrollmentStatus.Completed)
                {
                    kept++;

                    continue;
                }

                await _attemptStore.DeleteAsync(attempt, cancellationToken);
                context.Result.Count(AttemptKind);
            }

            foreach (var entry in await _overrideStore.ListByUserAsync(userId, cancellationToken))
            {
                await _overrideStore.DeleteAsync(entry, cancellationToken);
                context.Result.Count(OverrideKind);
            }

            if (kept > 0)
            {
                context.Result.Warnings.Add($"{kept} quiz attempts of completed enrollments were kept: they are the evidence of the training. An administrator decides whether to remove them.");
            }
        }
    }
}
