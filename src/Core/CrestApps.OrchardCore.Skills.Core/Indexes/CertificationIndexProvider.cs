using CrestApps.OrchardCore.Skills.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Skills.Core.Indexes;

/// <summary>
/// Maps certifications to <see cref="CertificationIndex"/> rows.
/// </summary>
public sealed class CertificationIndexProvider : IndexProvider<Certification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationIndexProvider"/> class.
    /// </summary>
    public CertificationIndexProvider()
    {
        CollectionName = SkillsConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<Certification> context)
    {
        context.For<CertificationIndex>()
            .Map(certification => new CertificationIndex
            {
                ItemId = certification.ItemId,
                Name = certification.Name,
                NormalizedName = SkillsNames.Normalize(certification.Name),
                Enabled = certification.Enabled,
            });
    }
}
