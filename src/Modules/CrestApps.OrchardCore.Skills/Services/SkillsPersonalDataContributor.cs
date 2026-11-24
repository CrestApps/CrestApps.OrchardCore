using System.Globalization;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Skills.Core.Services;
using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// Exports the skills and certificates a person holds as a user. Anonymizing deletes their skills and the certificates
/// recorded by hand, and keeps the certificates the site issued, without the name and evidence, because a verification
/// page or another organization may rely on them.
/// </summary>
internal sealed class SkillsPersonalDataContributor : IPersonalDataContributor
{
    private const string SkillKind = "SkillAssignment";
    private const string CertificateKind = "Certificate";

    private readonly SkillAssignmentStore _assignmentStore;
    private readonly CertificationRecordStore _recordStore;

    public SkillsPersonalDataContributor(SkillAssignmentStore assignmentStore, CertificationRecordStore recordStore)
    {
        _assignmentStore = assignmentStore;
        _recordStore = recordStore;
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        foreach (var userId in context.Subject.UserIds)
        {
            var subject = SkillSubject.User(userId);

            foreach (var assignment in await _assignmentStore.ListForSubjectAsync(subject, cancellationToken))
            {
                var record = context.Add(SkillKind, assignment.ItemId, assignment.SkillId);

                record.Fields["Skill"] = assignment.SkillId;
                record.Fields["Proficiency"] = assignment.Proficiency.ToString(CultureInfo.InvariantCulture);
                record.Fields["EffectiveFrom"] = assignment.EffectiveFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                record.Fields["EffectiveTo"] = assignment.EffectiveTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            foreach (var certificate in await _recordStore.ListForSubjectAsync(subject, cancellationToken))
            {
                var record = context.Add(CertificateKind, certificate.ItemId, certificate.Number ?? certificate.CertificationId, certificate.CreatedUtc);

                record.Fields["Certification"] = certificate.CertificationId;
                record.Fields["Name"] = certificate.SubjectName;
                record.Fields["Number"] = certificate.Number;
                record.Fields["IssuedOn"] = certificate.IssuedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                record.Fields["ExpiresOn"] = certificate.ExpiresOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                record.Fields["Status"] = certificate.Status.ToString();
                record.Fields["Evidence"] = certificate.Evidence;
                record.Fields["VerificationCode"] = certificate.VerificationCode;
                record.Fields["Notes"] = certificate.Notes;
            }
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        foreach (var userId in context.Subject.UserIds)
        {
            var subject = SkillSubject.User(userId);
            var kept = 0;

            foreach (var assignment in await _assignmentStore.ListForSubjectAsync(subject, cancellationToken))
            {
                await _assignmentStore.DeleteAsync(assignment, cancellationToken);
                context.Result.Count(SkillKind);
            }

            foreach (var certificate in await _recordStore.ListForSubjectAsync(subject, cancellationToken))
            {
                if (string.IsNullOrEmpty(certificate.VerificationCode))
                {
                    await _recordStore.DeleteAsync(certificate, cancellationToken);
                    context.Result.Count(CertificateKind);

                    continue;
                }

                certificate.SubjectName = null;
                certificate.Evidence = null;
                certificate.Notes = null;
                await _recordStore.UpdateAsync(certificate, cancellationToken);
                kept++;
            }

            if (kept > 0)
            {
                context.Result.Warnings.Add($"{kept} certificates the site issued were kept without the holder's name and evidence, because their verification page may be relied on. Revoke them to withdraw them.");
            }
        }
    }
}
