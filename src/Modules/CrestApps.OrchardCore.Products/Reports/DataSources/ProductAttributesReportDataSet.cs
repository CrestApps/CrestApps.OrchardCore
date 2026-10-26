using System.Linq.Expressions;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Indexes;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Products.Reports.DataSources;

/// <summary>
/// One row per attribute value of a listed product, such as <c>Make = Toyota</c> or a category the product is in: the
/// values the product listing filters and counts products by. They come from the search record the Product Listing
/// feature keeps for each published product, in each culture, and only those of the product types the principal may
/// view are read.
/// </summary>
public sealed class ProductAttributesReportDataSet : IndexedReportDataSet<ProductSearchDocument, ProductSearchIndex, ReportChildRow<ProductSearchDocument, ProductAttributeValue>>, IProductReportDataSet
{
    /// <summary>
    /// The name of the field that holds the identifier of the product a value belongs to.
    /// </summary>
    public const string ProductIdField = "ProductId";

    /// <summary>
    /// The name of the field that holds the product's content type.
    /// </summary>
    public const string ContentTypeField = "ContentType";

    private readonly ProductReportAccess _access;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductAttributesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the search records are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="access">Decides which products the principal may read.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ProductAttributesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        ProductReportAccess access,
        IStringLocalizer<ProductAttributesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                CommerceReportDataSets.ProductSource.Attributes,
                stringLocalizer["Product attributes"],
                stringLocalizer["One row per attribute value of a listed product, such as its make or a category it is in: the values the product listing filters products by."]),
            session,
            authorizationService,
            collection: null)
    {
        _access = access;

        var S = stringLocalizer;
        var product = S["Product"].Value;
        var attribute = S["Attribute"].Value;

        AddField(ProductIdField, S["Product ID"], DataFieldType.Text, row => row.Parent.ContentItemId, product).Identifier(CommerceReportDataSets.ProductReference());
        AddField("ProductTitle", S["Product"], DataFieldType.Text, row => row.Parent.Title, product);
        AddField(ContentTypeField, S["Product type"], DataFieldType.Text, row => row.Parent.ContentType, product);
        AddField(nameof(ProductSearchDocument.Culture), S["Culture"], DataFieldType.Text, row => row.Parent.Culture, product).Description = S["The culture of a localized product. Empty for a product that shows in every culture."].Value;

        AddField("AttributeKey", S["Attribute"], DataFieldType.Text, row => row.Child.Key, attribute).Description = S["The name of the field the value comes from, or a name the listing keeps for its own values, such as Product.Price."].Value;
        AddField("Value", S["Value"], DataFieldType.Text, row => row.Child.Text, attribute).Description = S["The value filters match on: the text itself, a term or content item identifier, or true or false."].Value;
        AddField(nameof(ProductAttributeValue.Label), S["Label"], DataFieldType.Text, row => row.Child.Label, attribute).Description = S["The text shown to visitors, such as a term's name."].Value;
        AddField("NumberValue", S["Number"], DataFieldType.Decimal, row => row.Child.Number, attribute);
        AddField("DateValue", S["Date"], DataFieldType.DateTime, row => row.Child.Date, attribute);
        AddField(nameof(ProductAttributeValue.Level), S["Level"], DataFieldType.Integer, row => row.Child.Level, attribute).Description = S["The depth of a taxonomy term, 1 for a top-level term, or 0 for a value that is not a term."].Value;

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<ProductSearchIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<ProductSearchIndex, string>>>(StringComparer.Ordinal)
        {
            [ProductIdField] = index => index.ContentItemId,
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

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<ProductSearchIndex> columns)
    {
        columns.Text(ContentTypeField, nameof(ProductSearchIndex.ContentType));
    }

    /// <inheritdoc/>
    protected override IEnumerable<ReportChildRow<ProductSearchDocument, ProductAttributeValue>> GetRows(ProductSearchDocument document)
    {
        return ReportChildRow<ProductSearchDocument, ProductAttributeValue>.For(document, document.Attributes?.Where(value => !string.IsNullOrEmpty(value?.Key)));
    }

    /// <inheritdoc/>
    protected override async Task<Func<IQueryIndex<ProductSearchIndex>, IQueryIndex<ProductSearchIndex>>> RestrictAsync(
        DataSourceContext context,
        CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(context?.User);

        if (access.IsEmpty)
        {
            return null;
        }

        // The search records are kept for published products only, so viewing a product type is enough to read them.
        var types = access.Types.ToArray();

        return rows => rows.Where(index => index.ContentType.IsIn(types));
    }
}
