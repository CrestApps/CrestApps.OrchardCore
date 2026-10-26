using System.Linq.Expressions;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Reviews.Indexes;
using CrestApps.OrchardCore.Reviews.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Reviews.Reports.DataSources;

/// <summary>
/// One row per review of a product: its stars, where its approval stands, whether the reviewer bought the product, and
/// the product, reviewer and order it is for. The title shows as the reviews page shows it; the text of the review and
/// its photos are never exposed.
/// </summary>
public sealed class ProductReviewsReportDataSet : IndexedReportDataSet<ProductReview, ProductReviewIndex>, IReviewReportDataSet
{
    /// <summary>
    /// The name of the field that holds when the review was written, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductReviewsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session reviews are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ProductReviewsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<ProductReviewsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.ReviewSource.Reviews,
                stringLocalizer["Product reviews"],
                stringLocalizer["One row per review of a product: its stars, approval status, whether the reviewer bought it, and the product, reviewer and order."]),
            session,
            authorizationService,
            ReviewsConstants.CollectionName,
            ReviewsPermissions.ManageReviews)
    {
        var S = stringLocalizer;
        var review = S["Review"].Value;
        var product = S["Product"].Value;
        var reviewer = S["Reviewer"].Value;
        var dates = S["Dates"].Value;

        AddField(CommerceReportDataSets.ItemIdField, S["Review ID"], DataFieldType.Text, record => record.ItemId, review).Identifier();
        AddField(nameof(ProductReview.Rating), S["Rating"], DataFieldType.Integer, record => record.Rating, review).Description = S["The stars given, from 1 to 5."].Value;
        AddField(nameof(ProductReview.Title), S["Title"], DataFieldType.Text, record => record.Title, review);
        AddField(nameof(ProductReview.Status), S["Status"], DataFieldType.Text, record => record.Status, review);
        AddField("IsApproved", S["Approved"], DataFieldType.Boolean, record => record.Status == ReviewStatus.Approved, review);
        AddField(nameof(ProductReview.VerifiedPurchase), S["Verified purchase"], DataFieldType.Boolean, record => record.VerifiedPurchase, review);
        AddField("PhotoCount", S["Photos"], DataFieldType.Integer, record => record.Photos?.Count ?? 0, review);

        AddField(nameof(ProductReview.ProductId), S["Product ID"], DataFieldType.Text, record => record.ProductId, product)
            .Identifier(CommerceReportDataSets.ProductReference());
        AddField(nameof(ProductReview.ProductTitle), S["Product"], DataFieldType.Text, record => record.ProductTitle, product);

        AddField(nameof(ProductReview.UserId), S["User ID"], DataFieldType.Text, record => record.UserId, reviewer).Identifier(ReportDataReferences.User());
        AddField(nameof(ProductReview.AuthorName), S["Reviewer name"], DataFieldType.Text, record => record.AuthorName, reviewer);
        AddField(nameof(ProductReview.OrderId), S["Order ID"], DataFieldType.Text, record => record.OrderId, reviewer)
            .Identifier(CommerceReportDataSets.Reference(CommerceReportDataSets.OrderSource.Name, CommerceReportDataSets.OrderSource.Orders));

        AddField(CreatedUtcField, S["Written"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField(nameof(ProductReview.ModifiedUtc), S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, dates);
        AddField(nameof(ProductReview.ModeratedUtc), S["Approved or rejected"], DataFieldType.DateTime, record => record.ModeratedUtc, dates);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<ProductReviewIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<ProductReviewIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<ProductReviewIndex, string>>>(StringComparer.Ordinal)
        {
            [CommerceReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(ProductReview.ProductId)] = index => index.ProductId,
            [nameof(ProductReview.UserId)] = index => index.UserId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<ProductReviewIndex> columns)
    {
        columns.Enum(nameof(ProductReview.Status), index => index.Status);
    }
}
