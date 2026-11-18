using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Certifications.Core.Indexes;

/// <summary>
/// Finds a certification path by its Skills certification.
/// </summary>
public sealed class CertificationPathIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the Skills certification.
    /// </summary>
    public string CertificationId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the path issues certificates.
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// One row per course or quiz a certification path asks for, so a completion finds its paths.
/// </summary>
public sealed class CertificationPathTargetIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the path.
    /// </summary>
    public string PathId { get; set; }

    /// <summary>
    /// Gets or sets the course or quiz.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the path issues certificates.
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// Finds a person's progress on a path, the progress of a certificate, and renewals that are due.
/// </summary>
public sealed class CertificationPathProgressIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the path.
    /// </summary>
    public string PathId { get; set; }

    /// <summary>
    /// Gets or sets the Skills certification.
    /// </summary>
    public string CertificationId { get; set; }

    /// <summary>
    /// Gets or sets the person.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets where the person stands.
    /// </summary>
    public CertificationPathState State { get; set; }

    /// <summary>
    /// Gets or sets the cycle.
    /// </summary>
    public int Cycle { get; set; }

    /// <summary>
    /// Gets or sets the current certificate.
    /// </summary>
    public string CurrentRecordId { get; set; }

    /// <summary>
    /// Gets or sets the expiry, as yyyyMMdd.
    /// </summary>
    public int? ExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets the day renewal opens, as yyyyMMdd.
    /// </summary>
    public int? RenewalOpensOn { get; set; }
}

/// <summary>
/// Indexes certification paths.
/// </summary>
public sealed class CertificationPathIndexProvider : IndexProvider<CertificationPath>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationPathIndexProvider"/> class.
    /// </summary>
    public CertificationPathIndexProvider()
    {
        CollectionName = CertificationsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<CertificationPath> context)
    {
        context.For<CertificationPathIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(path => new CertificationPathIndex
            {
                ItemId = path.ItemId,
                CertificationId = path.CertificationId,
                Enabled = path.Enabled,
            });

        context.For<CertificationPathTargetIndex>()
            .ExclusiveToDocumentType()
            .Map(path => path.InitialRequirements
                .Concat(path.RenewalRequirements)
                .Where(requirement => !string.IsNullOrEmpty(requirement.TargetId))
                .Select(requirement => requirement.TargetId)
                .Distinct(StringComparer.Ordinal)
                .Select(targetId => new CertificationPathTargetIndex
                {
                    PathId = path.ItemId,
                    TargetId = targetId,
                    Enabled = path.Enabled,
                }));
    }
}

/// <summary>
/// Indexes certification path progress.
/// </summary>
public sealed class CertificationPathProgressIndexProvider : IndexProvider<CertificationPathProgress>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationPathProgressIndexProvider"/> class.
    /// </summary>
    public CertificationPathProgressIndexProvider()
    {
        CollectionName = CertificationsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<CertificationPathProgress> context)
    {
        context.For<CertificationPathProgressIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(progress => new CertificationPathProgressIndex
            {
                ItemId = progress.ItemId,
                PathId = progress.PathId,
                CertificationId = progress.CertificationId,
                UserId = progress.UserId,
                State = progress.State,
                Cycle = progress.Cycle,
                CurrentRecordId = progress.CurrentRecordId,
                ExpiresOn = CertificationDates.ToKey(progress.ExpiresOn),
                RenewalOpensOn = CertificationDates.ToKey(progress.RenewalOpensOn),
            });
    }
}

/// <summary>
/// The index providers of certification paths.
/// </summary>
public static class CertificationsIndexProviders
{
    /// <summary>
    /// Lists them.
    /// </summary>
    /// <returns>The providers.</returns>
    public static IIndexProvider[] All() => [new CertificationPathIndexProvider(), new CertificationPathProgressIndexProvider(), new EducationCreditTypeIndexProvider(), new ExternalLearningSubmissionIndexProvider(), new CrestApps.OrchardCore.YesSql.Core.Ledgers.QuantityLedgerEntryIndexProvider(CertificationsConstants.CreditLedgerCollectionName)];
}
