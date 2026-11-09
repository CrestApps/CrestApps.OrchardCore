using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Reviews.Indexes;
using CrestApps.OrchardCore.Reviews.Models;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Reviews.Services;

/// <summary>
/// Exports a person's product reviews, found by their account, and anonymizes them: the author's name is replaced and
/// any mention of the person in the title or body is scrubbed. The rating stays, so the product's average does not
/// change.
/// </summary>
public sealed class ReviewPersonalDataContributor : ICommercePersonalDataContributor
{
    /// <summary>
    /// The kind of record reviews are exported as.
    /// </summary>
    public const string RecordKind = "ProductReview";

    /// <inheritdoc/>
    /// <remarks>
    /// The guest retention expiry never reached these records, so only an explicit erasure request does.
    /// </remarks>
    public bool TakesPartInRetentionExpiry => false;

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ReviewPersonalDataContributor(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public async Task IdentifyAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        foreach (var review in await FindAsync(subject, cancellationToken))
        {
            subject.AddName(review.AuthorName);
        }
    }

    /// <inheritdoc/>
    public async Task ExportAsync(CommercePersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var review in await FindAsync(context.Subject, cancellationToken))
        {
            context.Export.Records.Add(new CommercePersonalDataRecord
            {
                Kind = RecordKind,
                Id = review.ItemId,
                CreatedUtc = review.CreatedUtc,
                Values =
                {
                    [nameof(ProductReview.ProductTitle)] = review.ProductTitle,
                    [nameof(ProductReview.AuthorName)] = review.AuthorName,
                    [nameof(ProductReview.Rating)] = review.Rating.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    [nameof(ProductReview.Title)] = review.Title,
                    [nameof(ProductReview.Body)] = review.Body,
                },
            });
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(CommercePersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var review in await FindAsync(context.Subject, cancellationToken))
        {
            review.AuthorName = context.ReplaceName(review.AuthorName);
            review.Title = context.Scrub(review.Title);
            review.Body = context.Scrub(review.Body);

            await _session.SaveAsync(review, collection: ReviewsConstants.CollectionName, cancellationToken: cancellationToken);
            context.Result.Count(RecordKind);
        }
    }

    private async Task<IReadOnlyList<ProductReview>> FindAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var userIds = subject.UserIds.Where(userId => !string.IsNullOrEmpty(userId)).ToArray();

        if (userIds.Length == 0)
        {
            return [];
        }

        return [.. await _session.Query<ProductReview, ProductReviewIndex>(index => index.UserId.IsIn(userIds), collection: ReviewsConstants.CollectionName).ListAsync(cancellationToken)];
    }
}
