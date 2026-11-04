using CrestApps.OrchardCore.Skills.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Skills.Core.Indexes;

/// <summary>
/// Maps certification records to <see cref="CertificationRecordIndex"/> rows.
/// </summary>
public sealed class CertificationRecordIndexProvider : IndexProvider<CertificationRecord>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationRecordIndexProvider"/> class.
    /// </summary>
    public CertificationRecordIndexProvider()
    {
        CollectionName = SkillsConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<CertificationRecord> context)
    {
        context.For<CertificationRecordIndex>()
            .Map(record => new CertificationRecordIndex
            {
                ItemId = record.ItemId,
                SubjectKind = record.SubjectKind,
                SubjectId = record.SubjectId,
                SubjectName = record.SubjectName,
                CertificationId = record.CertificationId,
                Number = record.Number,
                Status = record.Status,
                IssuedOn = SkillsDates.ToKey(record.IssuedOn),
                ExpiresOn = SkillsDates.ToKey(record.ExpiresOn),
            });
    }
}
