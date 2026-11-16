using System.Globalization;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Coursework.Core.Services;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Learning.Coursework.Services;

/// <summary>
/// Exports a person's coursework submissions with their file names. Anonymizing deletes the submitted files from the
/// protected store and clears the text, the file list and every feedback and reason, but keeps each submission's
/// state, dates and grade: the course grade of the enrollment rests on them.
/// </summary>
internal sealed class CourseworkPersonalDataContributor : IPersonalDataContributor
{
    internal const string Kind = "LearningSubmission";

    private readonly SubmissionStore _store;
    private readonly IProtectedFileStore<CourseworkFiles> _files;

    public CourseworkPersonalDataContributor(SubmissionStore store, IProtectedFileStore<CourseworkFiles> files)
    {
        _store = store;
        _files = files;
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
                context.Result.Warnings.Add($"{kept} coursework submissions were kept with their dates and grades, because the course grade rests on them; their text, files and feedback were removed.");
            }
        }
    }
}
