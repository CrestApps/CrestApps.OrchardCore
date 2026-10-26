using System.Linq.Expressions;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DataSources;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Products.Reports.DataSources;

/// <summary>
/// A data set whose rows come from products, the content items of the content types that carry the product part. They
/// are read through the content item index a page at a time, newest first, and only those of the product types the
/// principal may view: the latest version of each product of a type the principal may edit, else the published one, so
/// a product gives its rows once whatever number of versions it has. A join on the product's identifier narrows the
/// read. The data set never groups in the database, because which products a principal reads depends on the principal.
/// </summary>
/// <typeparam name="TRow">The type of the rows of the data set.</typeparam>
public abstract class ProductContentReportDataSet<TRow> : IndexedReportDataSet<ContentItem, ContentItemIndex, TRow>, IProductReportDataSet
    where TRow : class
{
    /// <summary>
    /// The name of the field that holds the product's content type.
    /// </summary>
    public const string ContentTypeField = "ContentType";

    private readonly ProductReportAccess _access;
    private readonly string _productIdField;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductContentReportDataSet{TRow}"/> class.
    /// </summary>
    /// <param name="descriptor">The data set's descriptor.</param>
    /// <param name="session">The YesSql session products are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="access">Decides which products the principal may read.</param>
    /// <param name="productIdField">The name of the field that holds the product's content item identifier.</param>
    protected ProductContentReportDataSet(
        DataSetDescriptor descriptor,
        ISession session,
        IAuthorizationService authorizationService,
        ProductReportAccess access,
        string productIdField)
        : base(descriptor, session, authorizationService, collection: null)
    {
        _access = access;
        _productIdField = productIdField;
    }

    /// <summary>
    /// Gets the product types the principal of the current read may read, and their product settings. It is set when a
    /// read starts, before the first row is produced.
    /// </summary>
    protected ProductTypeAccess Types { get; private set; } = ProductTypeAccess.None;

    /// <inheritdoc/>
    protected override bool CanAggregate => false;

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<ContentItemIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<ContentItemIndex, string>>>(StringComparer.Ordinal)
        {
            [_productIdField] = index => index.ContentItemId,
        };

    /// <inheritdoc/>
    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        if (!await base.CanReadAsync(context, cancellationToken))
        {
            return false;
        }

        return !(await _access.GetAsync(context.User)).IsEmpty;
    }

    /// <summary>
    /// Gets the currency a product's prices are in.
    /// </summary>
    /// <param name="product">The product.</param>
    /// <returns>The currency code, or <see langword="null"/> when the product has none.</returns>
    protected string Currency(ContentItem product)
    {
        return ProductReportValues.Currency(ProductReportValues.Part<ProductPart>(product), Types.GetSettings(product?.ContentType));
    }

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<ContentItemIndex> columns)
    {
        columns.Text(ContentTypeField, nameof(ContentItemIndex.ContentType));
    }

    /// <inheritdoc/>
    protected override async Task<Func<IQueryIndex<ContentItemIndex>, IQueryIndex<ContentItemIndex>>> RestrictAsync(
        DataSourceContext context,
        CancellationToken cancellationToken)
    {
        var types = await _access.GetAsync(context?.User);

        Types = types;

        return types.IsEmpty ? null : types.Restrict;
    }
}
