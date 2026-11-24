using System.Globalization;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Programs.Core.Services;
using CrestApps.OrchardCore.PersonalData;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Learning.Programs.Services;

/// <summary>
/// Exports the learning paths and required learning states of a person with their exemptions, and deletes them, with
/// the evidence files of the exemptions, when the person is erased.
/// </summary>
internal sealed class ProgramsPersonalDataContributor : IPersonalDataContributor
{
    private const string PathKind = "LearningPathProgress";
    private const string RequiredKind = "RequiredLearning";

    private readonly LearningPathProgressStore _paths;
    private readonly IServiceProvider _services;

    public ProgramsPersonalDataContributor(LearningPathProgressStore paths, IServiceProvider services)
    {
        _paths = paths;
        _services = services;
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        foreach (var userId in context.Subject.UserIds)
        {
            foreach (var progress in await _paths.ListByUserAsync(userId, cancellationToken))
            {
                var record = context.Add(PathKind, progress.ItemId, progress.PathId, progress.StartedUtc);

                record.Fields["Path"] = progress.PathId;
                record.Fields["State"] = progress.State.ToString();
                record.Fields["CompletedCourses"] = string.Join(", ", progress.CompletedCourseIds);
                record.Fields["CompletedUtc"] = progress.CompletedUtc?.ToString("O", CultureInfo.InvariantCulture);
            }
        }

        if (_services.GetService<RequiredLearningAssignmentStore>() is not { } assignments)
        {
            return;
        }

        foreach (var assignment in await assignments.ListByUsersAsync(context.Subject.UserIds.ToArray(), cancellationToken))
        {
            var record = context.Add(RequiredKind, assignment.ItemId, assignment.RequirementId, assignment.RequiredUtc);

            record.Fields["Requirement"] = assignment.RequirementId;
            record.Fields["State"] = assignment.State.ToString();
            record.Fields["DueUtc"] = assignment.DueUtc?.ToString("O", CultureInfo.InvariantCulture);
            record.Fields["CompletedUtc"] = assignment.CompletedUtc?.ToString("O", CultureInfo.InvariantCulture);
            record.Fields["Exemption"] = assignment.Exemption?.Reason;
            record.Fields["Evidence"] = assignment.Exemption?.EvidenceFileName;
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        foreach (var userId in context.Subject.UserIds)
        {
            foreach (var progress in await _paths.ListByUserAsync(userId, cancellationToken))
            {
                await _paths.DeleteAsync(progress, cancellationToken);
                context.Result.Count(PathKind);
            }
        }

        if (_services.GetService<RequiredLearningAssignmentStore>() is not { } assignments)
        {
            return;
        }

        var files = _services.GetService<IProtectedFileStore<RequiredLearningEvidence>>();

        foreach (var assignment in await assignments.ListByUsersAsync(context.Subject.UserIds.ToArray(), cancellationToken))
        {
            if (files is not null && !string.IsNullOrEmpty(assignment.Exemption?.EvidenceFileId))
            {
                await files.DeleteAsync(assignment.Exemption.EvidenceFileId, cancellationToken);
            }

            await assignments.DeleteAsync(assignment, cancellationToken);
            context.Result.Count(RequiredKind);
        }
    }
}
