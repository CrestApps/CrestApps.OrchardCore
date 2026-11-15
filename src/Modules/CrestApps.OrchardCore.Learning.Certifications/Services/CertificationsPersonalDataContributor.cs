using System.Globalization;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using CrestApps.OrchardCore.Ledgers;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Learning.Certifications.Services;

/// <summary>
/// Exports a person's progress on certification paths with every cycle and its evidence; anonymizing deletes the
/// progress. The certificates themselves belong to Skills, which keeps issued ones without the name.
/// </summary>
internal sealed class CertificationsPersonalDataContributor : IPersonalDataContributor
{
    private const string Kind = "LearningCertificationProgress";

    private const string CreditKind = "LearningEducationCredit";
    private const string ExternalKind = "LearningExternalLearning";

    private readonly CertificationPathProgressStore _progress;
    private readonly IQuantityLedger<EducationCreditLedger> _credits;
    private readonly ExternalLearningSubmissionStore _external;
    private readonly IProtectedFileStore<ExternalLearningEvidence> _evidence;

    public CertificationsPersonalDataContributor(
        CertificationPathProgressStore progress,
        IQuantityLedger<EducationCreditLedger> credits,
        ExternalLearningSubmissionStore external,
        IProtectedFileStore<ExternalLearningEvidence> evidence)
    {
        _progress = progress;
        _credits = credits;
        _external = external;
        _evidence = evidence;
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

            foreach (var entry in await _credits.ListForAccountAsync(userId, cancellationToken))
            {
                var record = context.Add(CreditKind, entry.ItemId, entry.Note ?? entry.Bucket, entry.CreatedUtc);

                record.Fields["CreditType"] = entry.Bucket;
                record.Fields["Amount"] = entry.Amount.ToString(CultureInfo.InvariantCulture);
                record.Fields["EarnedOn"] = entry.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                record.Fields["ExpiresOn"] = entry.ExpiresOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                record.Fields["Source"] = entry.Kind;
            }

            foreach (var submission in await _external.ListByUserAsync(userId, cancellationToken))
            {
                var record = context.Add(ExternalKind, submission.ItemId, submission.Title, submission.SubmittedUtc);

                record.Fields["Title"] = submission.Title;
                record.Fields["Provider"] = submission.Provider;
                record.Fields["CompletedOn"] = submission.CompletedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                record.Fields["CreditType"] = submission.CreditTypeId;
                record.Fields["Amount"] = submission.Amount.ToString(CultureInfo.InvariantCulture);
                record.Fields["State"] = submission.State.ToString();
                record.Fields["Evidence"] = submission.EvidenceFileName;
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

            for (var deleted = await _credits.DeleteAccountAsync(userId, cancellationToken); deleted > 0; deleted--)
            {
                context.Result.Count(CreditKind);
            }

            foreach (var submission in await _external.ListByUserAsync(userId, cancellationToken))
            {
                if (!string.IsNullOrEmpty(submission.EvidenceFileId))
                {
                    await _evidence.DeleteAsync(submission.EvidenceFileId, cancellationToken);
                }

                await _external.DeleteAsync(submission, cancellationToken);
                context.Result.Count(ExternalKind);
            }
        }
    }
}
