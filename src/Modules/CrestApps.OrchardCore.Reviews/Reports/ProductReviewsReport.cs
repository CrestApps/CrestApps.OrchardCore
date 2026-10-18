using System.Globalization;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reviews.Indexes;
using CrestApps.OrchardCore.Reviews.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Reviews.Reports;

/// <summary>
/// The reviews written in the period: how many wait for approval, the stars given, and each product's rating.
/// </summary>
public sealed class ProductReviewsReport : IReport
{
    private const int PageSize = 1000;

    private static readonly DateTime _earliest = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public ProductReviewsReport(ISession session, IClock clock, IStringLocalizer<ProductReviewsReport> stringLocalizer)
    {
        _session = session;
        _clock = clock;
        S = stringLocalizer;
    }

    public string Name => "product-reviews";

    public LocalizedString DisplayName => S["Product reviews"];

    public LocalizedString Description => S["The reviews written in the period: how many wait for approval, the stars given, and each product's average rating from its approved reviews."];

    public string Category => ReportsConstants.Categories.Commerce;

    public Permission Permission => ReviewsPermissions.ManageReviews;

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var from = range.FromUtc ?? _earliest;
        var to = range.ToUtc ?? _clock.UtcNow;
        var reviews = new List<ProductReview>();

        for (var skip = 0; ; skip += PageSize)
        {
            var page = (await _session
                .Query<ProductReview, ProductReviewIndex>(index => index.CreatedUtc >= from && index.CreatedUtc < to, collection: ReviewsConstants.CollectionName)
                .OrderBy(index => index.Id)
                .Skip(skip)
                .Take(PageSize)
                .ListAsync(cancellationToken)).ToList();

            reviews.AddRange(page);

            if (page.Count < PageSize)
            {
                break;
            }
        }

        var approved = reviews.Where(review => review.Status == ReviewStatus.Approved).ToList();
        var document = new ReportDocument { Title = DisplayName.Value };

        document.Add(ReportSection.ForMetrics(S["Summary"].Value,
        [
            new ReportMetric(S["Reviews written"].Value, ReportFormat.Number(reviews.Count)),
            new ReportMetric(S["Waiting for approval"].Value, ReportFormat.Number(reviews.Count(review => review.Status == ReviewStatus.Pending))),
            new ReportMetric(S["Approved"].Value, ReportFormat.Number(approved.Count)),
            new ReportMetric(S["Rejected"].Value, ReportFormat.Number(reviews.Count(review => review.Status == ReviewStatus.Rejected))),
            new ReportMetric(S["Average rating"].Value, approved.Count == 0 ? S["n/a"].Value : approved.Average(review => review.Rating).ToString("0.0", CultureInfo.CurrentCulture), S["From the approved reviews."].Value),
            new ReportMetric(S["Verified purchases"].Value, Share(reviews.Count(review => review.VerifiedPurchase), reviews.Count)),
        ]));

        if (reviews.Count == 0)
        {
            return document;
        }

        var stars = Enumerable.Range(1, ReviewsConstants.MaxRating).Reverse().Select(rating => (Rating: rating, Count: reviews.Count(review => review.Rating == rating))).ToArray();
        var most = Math.Max(1, stars.Max(star => star.Count));

        document.Add(ReportSection.ForBars(S["Stars given"].Value,
            [.. stars.Select(star => new ReportBar(S.Plural(star.Rating, "1 star", "{0} stars").Value, ReportFormat.Number(star.Count), (double)star.Count / most))]));

        document.Add(ReportSection.ForTable(S["Products"].Value,
        [
            new ReportColumn(S["Product"].Value),
            new ReportColumn(S["Reviews"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Waiting"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Approved"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Average rating"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Verified"].Value, ReportColumnAlign.End),
        ], [.. reviews
            .GroupBy(review => review.ProductId ?? string.Empty, StringComparer.Ordinal)
            .Select(group => (Title: group.OrderByDescending(review => review.CreatedUtc).First().ProductTitle, Reviews: group.ToList()))
            .OrderByDescending(product => product.Reviews.Count)
            .ThenBy(product => product.Title, StringComparer.OrdinalIgnoreCase)
            .Select(product =>
            {
                var productApproved = product.Reviews.Where(review => review.Status == ReviewStatus.Approved).ToList();

                return new ReportRow(
                [
                    product.Title ?? string.Empty,
                    ReportFormat.Number(product.Reviews.Count),
                    ReportFormat.Number(product.Reviews.Count(review => review.Status == ReviewStatus.Pending)),
                    ReportFormat.Number(productApproved.Count),
                    productApproved.Count == 0 ? S["n/a"].Value : productApproved.Average(review => review.Rating).ToString("0.0", CultureInfo.CurrentCulture),
                    Share(product.Reviews.Count(review => review.VerifiedPurchase), product.Reviews.Count),
                ]);
            })]));

        return document;
    }

    private static string Share(int part, int whole)
        => whole == 0 ? "0%" : ((double)part / whole).ToString("P0", CultureInfo.CurrentCulture);
}
