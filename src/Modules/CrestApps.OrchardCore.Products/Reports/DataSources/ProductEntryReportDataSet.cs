using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.ContentManagement;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Products.Reports.DataSources;

/// <summary>
/// A data set with one row per entry a product holds, such as its variants or its named prices. Each row carries the
/// product's identifier, title, content type and whether it is the published version, so a report can group the entries
/// by them without joining the products.
/// </summary>
/// <typeparam name="TEntry">The type of the entries.</typeparam>
public abstract class ProductEntryReportDataSet<TEntry> : ProductContentReportDataSet<ReportChildRow<ContentItem, TEntry>>
    where TEntry : class
{
    /// <summary>
    /// The name of the field that holds the identifier of the product an entry belongs to.
    /// </summary>
    public const string ProductIdField = "ProductId";

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductEntryReportDataSet{TEntry}"/> class.
    /// </summary>
    /// <param name="descriptor">The data set's descriptor.</param>
    /// <param name="session">The YesSql session products are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="access">Decides which products the principal may read.</param>
    /// <param name="productIdField">The name of the field that holds the identifier of the product an entry belongs
    /// to.</param>
    protected ProductEntryReportDataSet(
        DataSetDescriptor descriptor,
        ISession session,
        IAuthorizationService authorizationService,
        ProductReportAccess access,
        string productIdField)
        : base(descriptor, session, authorizationService, access, productIdField)
    {
    }

    /// <summary>
    /// Declares the fields of the product an entry belongs to.
    /// </summary>
    /// <param name="idField">The name of the field that holds the product's identifier.</param>
    /// <param name="idName">The label of the product's identifier.</param>
    /// <param name="titleField">The name of the field that holds the product's title.</param>
    /// <param name="titleName">The label of the product's title.</param>
    /// <param name="typeName">The label of the product's content type.</param>
    /// <param name="publishedName">The label of whether the product's version is the published one.</param>
    /// <param name="group">The group the fields are listed under.</param>
    protected void AddProductFields(
        string idField,
        string idName,
        string titleField,
        string titleName,
        string typeName,
        string publishedName,
        string group)
    {
        AddField(idField, idName, DataFieldType.Text, row => row.Parent.ContentItemId, group).Identifier(CommerceReportDataSets.ProductReference());
        AddField(titleField, titleName, DataFieldType.Text, row => row.Parent.DisplayText, group);
        AddField(ContentTypeField, typeName, DataFieldType.Text, row => row.Parent.ContentType, group);
        AddField("Published", publishedName, DataFieldType.Boolean, row => row.Parent.Published, group);
    }

    /// <summary>
    /// Gets the entries a product holds.
    /// </summary>
    /// <param name="product">The product.</param>
    /// <returns>The entries, or <see langword="null"/> when it holds none.</returns>
    protected abstract IEnumerable<TEntry> GetEntries(ContentItem product);

    /// <inheritdoc/>
    protected sealed override IEnumerable<ReportChildRow<ContentItem, TEntry>> GetRows(ContentItem document)
    {
        return ReportChildRow<ContentItem, TEntry>.For(document, GetEntries(document));
    }
}
