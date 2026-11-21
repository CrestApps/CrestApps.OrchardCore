using System.Globalization;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Learning.Packages.Services;

/// <summary>
/// Exports a person's runtime state on each learning package. Erasure deletes every runtime state that is not evidence
/// of a completed enrollment (Learning deletes those enrollments), as quiz attempts and coursework submissions are; a
/// runtime state of a completed enrollment keeps its status, score, time and dates and loses the learner's name and id,
/// the suspend data, the location, the comments (the learner's comments in SCORM 2004), the language preference and the
/// interactions. A SCORM 2004 state is exported with its standard, attempts and evaluated statuses.
/// </summary>
internal sealed class PackagesPersonalDataContributor : IPersonalDataContributor
{
    internal const string Kind = "LearningPackageRuntime";

    private readonly PackageRuntimeStore _store;
    private readonly IEnrollmentStore _enrollments;

    public PackagesPersonalDataContributor(PackageRuntimeStore store, IEnrollmentStore enrollments)
    {
        _store = store;
        _enrollments = enrollments;
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        foreach (var userId in context.Subject.UserIds)
        {
            foreach (var state in await _store.ListByUserAsync(userId, cancellationToken))
            {
                var record = context.Add(Kind, state.ItemId, state.PackageTitle ?? state.PackageItemId, state.FirstLaunchedUtc);

                record.Fields["Package"] = state.PackageItemId;
                record.Fields["Course"] = state.CourseId;
                record.Fields["Standard"] = state.Standard ?? PackagesConstants.Standards.Scorm12;
                record.Fields["Sessions"] = state.Sessions.ToString(CultureInfo.InvariantCulture);

                if (PackagesConstants.Standards.IsScorm2004(state.Standard))
                {
                    var data = state.Cmi2004 ?? new Scorm2004Cmi();

                    record.Fields["Attempts"] = state.Attempts.ToString(CultureInfo.InvariantCulture);
                    record.Fields["CompletionStatus"] = Scorm2004DataModel.EvaluateCompletionStatus(data);
                    record.Fields["SuccessStatus"] = Scorm2004DataModel.EvaluateSuccessStatus(data);
                    record.Fields["Score"] = PackageResultMapper.ScorePercent(data)?.ToString("0.##", CultureInfo.InvariantCulture);
                    record.Fields["TotalTime"] = data.TotalTime;
                    record.Fields["Location"] = data.Location;
                    record.Fields["SuspendData"] = data.SuspendData;
                    record.Fields["Comments"] = string.Join("; ", data.CommentsFromLearner.Select(comment => comment.Comment));
                    record.Fields["Interactions"] = string.Join("; ", data.Interactions.Select(interaction => $"{interaction.Id}: {interaction.LearnerResponse} ({interaction.Result})"));

                    continue;
                }

                var cmi = state.Cmi ?? new Scorm12Cmi();

                record.Fields["LessonStatus"] = cmi.LessonStatus;
                record.Fields["Score"] = cmi.ScoreRaw;
                record.Fields["TotalTime"] = cmi.TotalTime;
                record.Fields["LessonLocation"] = cmi.LessonLocation;
                record.Fields["SuspendData"] = cmi.SuspendData;
                record.Fields["Comments"] = cmi.Comments;
                record.Fields["Interactions"] = string.Join("; ", cmi.Interactions.Select(interaction => $"{interaction.Id}: {interaction.StudentResponse} ({interaction.Result})"));
            }
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        foreach (var userId in context.Subject.UserIds)
        {
            var kept = 0;

            foreach (var state in await _store.ListByUserAsync(userId, cancellationToken))
            {
                var enrollment = string.IsNullOrEmpty(state.EnrollmentId) ? null : await _enrollments.FindByIdAsync(state.EnrollmentId, cancellationToken);

                // As for quiz attempts and coursework: only what a completed enrollment rests on is kept.
                if (enrollment?.Status != EnrollmentStatus.Completed)
                {
                    await _store.DeleteAsync(state, cancellationToken);
                    context.Result.Count(Kind);

                    continue;
                }

                if (PackagesConstants.Standards.IsScorm2004(state.Standard))
                {
                    var data = state.Cmi2004 ??= new Scorm2004Cmi();

                    data.LearnerId = string.Empty;
                    data.LearnerName = string.Empty;
                    data.Location = null;
                    data.SuspendData = null;
                    data.Language = string.Empty;
                    data.CommentsFromLearner = [];
                    data.Interactions = [];
                }
                else
                {
                    var cmi = state.Cmi ??= new Scorm12Cmi();

                    cmi.StudentId = string.Empty;
                    cmi.StudentName = string.Empty;
                    cmi.LessonLocation = string.Empty;
                    cmi.SuspendData = string.Empty;
                    cmi.Comments = string.Empty;
                    cmi.Language = string.Empty;
                    cmi.Interactions = [];
                }

                state.CurrentLaunchId = null;

                await _store.UpdateAsync(state, cancellationToken);
                context.Result.Count(Kind);
                kept++;
            }

            if (kept > 0)
            {
                context.Result.Warnings.Add($"{kept} learning package runtime states of completed enrollments were kept with their status, score and time, because the completion rests on them; the learner's name, suspend data, location, comments and interactions were removed.");
            }
        }
    }
}
