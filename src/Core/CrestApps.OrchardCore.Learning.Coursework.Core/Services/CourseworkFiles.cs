using System.Security.Claims;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;

namespace CrestApps.OrchardCore.Learning.Coursework.Core.Services;

/// <summary>
/// The protected file area of the files learners hand in as coursework.
/// </summary>
public sealed class CourseworkFiles
{
}

/// <summary>
/// Opens a submitted file to the learner who handed it in and to the people who may grade their work in the course:
/// everyone with Grade assessments, or the course's graders, instructors and assistants with Manage the courses you
/// teach. The file must belong to the submission named in the link.
/// </summary>
internal sealed class CourseworkFilesPolicy : IProtectedFileAccessPolicy<CourseworkFiles>
{
    private readonly SubmissionStore _store;
    private readonly GradingScope _scope;

    public CourseworkFilesPolicy(SubmissionStore store, GradingScope scope)
    {
        _store = store;
        _scope = scope;
    }

    public async Task<bool> CanReadAsync(ProtectedFileAccess access, CancellationToken cancellationToken = default)
    {
        if (access?.User is null || string.IsNullOrEmpty(access.ResourceId) || string.IsNullOrEmpty(access.FileId))
        {
            return false;
        }

        var submission = await _store.FindByIdAsync(access.ResourceId, cancellationToken);

        if (submission is null || !submission.Files.Any(file => string.Equals(file.FileId, access.FileId, StringComparison.Ordinal)))
        {
            return false;
        }

        if (string.Equals(submission.UserId, access.User.FindFirstValue(ClaimTypes.NameIdentifier), StringComparison.Ordinal))
        {
            return true;
        }

        return await _scope.CanGradeAsync(access.User, submission.UserId, submission.CourseId);
    }
}
