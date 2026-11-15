using System.Globalization;
using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Learning.Certifications.Services;

/// <summary>
/// Exports a person's progress on certification paths with every cycle and its evidence; anonymizing deletes the
/// progress. The certificates themselves belong to Skills, which keeps issued ones without the name.
/// </summary>
internal sealed class CertificationsPersonalDataContributor : IPersonalDataContributor
{
    private const string Kind = "LearningCertificationProgress";

    private readonly CertificationPathProgressStore _progress;

    public CertificationsPersonalDataContributor(CertificationPathProgressStore progress)
    {
        _progress = progress;
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        foreach (var userId in context.Subject.UserIds)
        {
            foreach (var progress in await _progress.ListForUserAsync(userId, cancellationToken))
            {
                var record = context.Add(Kind, progress.ItemId, progress.CertificationId, progress.CreatedUtc);

                record.Fields["Certification"] = progress.CertificationId;
                record.Fields["State"] = progress.State.ToString();
                record.Fields["Cycle"] = progress.Cycle.ToString(CultureInfo.InvariantCulture);
                record.Fields["ExpiresOn"] = progress.ExpiresOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                record.Fields["Cycles"] = string.Join(" | ", progress.Cycles.Select(cycle =>
                    $"{cycle.Number}: {string.Join(", ", cycle.Evidence.Select(evidence => evidence.Title ?? evidence.TargetId))}{(cycle.RecordId is null ? string.Empty : " -> " + cycle.RecordId)}"));
            }
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        foreach (var userId in context.Subject.UserIds)
        {
            foreach (var progress in await _progress.ListForUserAsync(userId, cancellationToken))
            {
                await _progress.DeleteAsync(progress, cancellationToken);
                context.Result.Count(Kind);
            }
        }
    }
}
