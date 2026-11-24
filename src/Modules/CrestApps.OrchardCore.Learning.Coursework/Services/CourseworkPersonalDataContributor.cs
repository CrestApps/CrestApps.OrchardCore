using System.Globalization;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Coursework.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Learning.Coursework.Services;

/// <summary>
/// Exports a person's coursework submissions with their file names. Anonymizing deletes the submitted files from the
/// protected store, and deletes every submission that is not evidence of a completed enrollment (Learning deletes those
/// enrollments), as quiz attempts are. A submission of a completed enrollment keeps its state, dates and grade, which
/// the course grade rests on, with its text, file list and every feedback and reason cleared.
/// </summary>
internal sealed class CourseworkPersonalDataContributor : IPersonalDataContributor
{
    internal const string Kind = "LearningSubmission";

    private readonly SubmissionStore _store;
    private readonly IProtectedFileStore<CourseworkFiles> _files;
    private readonly IEnrollmentStore _enrollments;

    public CourseworkPersonalDataContributor(SubmissionStore store, IProtectedFileStore<CourseworkFiles> files, IEnrollmentStore enrollments)
    {
        _store = store;
        _files = files;
        _enrollments = enrollments;
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        foreach (var userId in context.Subject.UserIds)
        {
            foreach (var submission in await _store.ListByUserAsync(userId, cancellationToken))
            {
                var record = context.Add(Kind, submission.ItemId, submission.CourseworkTitle ?? submission.CourseworkId, submission.SubmittedUtc);

                record.Fields["Coursework"] = submission.CourseworkId;
                record.Fields["Course"] = submission.CourseId;
                record.Fields["Attempt"] = submission.Attempt.ToString(CultureInfo.InvariantCulture);
                record.Fields["Status"] = submission.Status.ToString();
                record.Fields["Late"] = submission.Late ? "Yes" : "No";
                record.Fields["Text"] = submission.Text;
                record.Fields["Files"] = string.Join(", ", submission.Files.Select(file => file.FileName));
                record.Fields["Score"] = submission.Score?.ToString(CultureInfo.InvariantCulture);
                record.Fields["Feedback"] = submission.Feedback;
                record.Fields["GradedUtc"] = submission.GradedUtc?.ToString("O", CultureInfo.InvariantCulture);
            }
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        foreach (var userId in context.Subject.UserIds)
        {
            var kept = 0;

            foreach (var submission in await _store.ListByUserAsync(userId, cancellationToken))
            {
                foreach (var file in submission.Files)
                {
                    await _files.DeleteAsync(file.FileId, cancellationToken);
                }

                var enrollment = string.IsNullOrEmpty(submission.EnrollmentId) ? null : await _enrollments.FindByIdAsync(submission.EnrollmentId, cancellationToken);

                // As for quiz attempts: only work a completed enrollment rests on is kept, as evidence of the training.
                // Learning deletes the enrollments that are not completed, so a submission of one would otherwise stay
                // in the grading queue as an empty row that reports to no enrollment.
                if (enrollment?.Status != EnrollmentStatus.Completed)
                {
                    await _store.DeleteAsync(submission, cancellationToken);
                    context.Result.Count(Kind);

                    continue;
                }

                submission.Files = [];
                submission.Text = null;
                submission.Feedback = null;

                foreach (var grade in submission.History)
                {
                    grade.Feedback = null;
                    grade.Reason = null;
                }

                await _store.UpdateAsync(submission, cancellationToken);
                context.Result.Count(Kind);
                kept++;
            }

            if (kept > 0)
            {
                context.Result.Warnings.Add($"{kept} coursework submissions of completed enrollments were kept with their dates and grades, because the course grade rests on them; their text, files and feedback were removed.");
            }
        }
    }
}
