using CrestApps.OrchardCore.DataConnectors.Connectors.Http;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.Models;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.DataSources;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.DataConnectors.ViewModels;

/// <summary>
/// The search of an admin list.
/// </summary>
public class DataConnectorListOptions
{
    /// <summary>
    /// Gets or sets the text searched for.
    /// </summary>
    public string Search { get; set; }
}

/// <summary>
/// The connections list.
/// </summary>
public class DataConnectionsIndexViewModel
{
    /// <summary>
    /// Gets or sets the search.
    /// </summary>
    public DataConnectorListOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }

    /// <summary>
    /// Gets or sets the picker of the connector a new connection uses.
    /// </summary>
    [BindNever]
    public DataConnectorPickerViewModel Picker { get; set; }
}

/// <summary>
/// The connected data sets list.
/// </summary>
public class ConnectedDataSetsIndexViewModel
{
    /// <summary>
    /// Gets or sets the search.
    /// </summary>
    public DataConnectorListOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }

    /// <summary>
    /// Gets or sets the picker of the connection a new data set reads.
    /// </summary>
    [BindNever]
    public DataConnectorPickerViewModel Picker { get; set; }
}

/// <summary>
/// A searchable picker of cards in a modal, grouped by category, like the steps of a deployment plan.
/// </summary>
public class DataConnectorPickerViewModel
{
    /// <summary>
    /// Gets or sets the modal's element identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the modal's title.
    /// </summary>
    public IHtmlContent Title { get; set; }

    /// <summary>
    /// Gets or sets the message shown when there is nothing to pick.
    /// </summary>
    public IHtmlContent EmptyMessage { get; set; }

    /// <summary>
    /// Gets or sets the categories the cards are grouped in, or none to show only the search.
    /// </summary>
    public IList<DataConnectorPickerCategory> Categories { get; set; } = [];

    /// <summary>
    /// Gets or sets the cards.
    /// </summary>
    public IList<DataConnectorPickerItem> Items { get; set; } = [];
}

/// <summary>
/// A category of a picker.
/// </summary>
public class DataConnectorPickerCategory
{
    /// <summary>
    /// Gets or sets the category's identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the category's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the number of cards in the category.
    /// </summary>
    public int Count { get; set; }
}

/// <summary>
/// A card of a picker.
/// </summary>
public class DataConnectorPickerItem
{
    /// <summary>
    /// Gets or sets the card's title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the card's description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the badge under the title, such as the connector.
    /// </summary>
    public string Badge { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the card's category.
    /// </summary>
    public string CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the address the card's button opens.
    /// </summary>
    public string Url { get; set; }
}

/// <summary>
/// The page that creates or edits a connection or a data set.
/// </summary>
public class DataConnectorEditViewModel
{
    /// <summary>
    /// Gets or sets the page's title.
    /// </summary>
    public IHtmlContent Title { get; set; }

    /// <summary>
    /// Gets or sets the record's identifier, or <see langword="null"/> for a new one.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the editor shape.
    /// </summary>
    public IShape Editor { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the record is a data set, rather than a connection.
    /// </summary>
    public bool IsDataSet { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the data set is imported.
    /// </summary>
    public bool IsImported { get; set; }
}

/// <summary>
/// The fields every connection has.
/// </summary>
public class DataConnectionFieldsViewModel
{
    /// <summary>
    /// Gets or sets the name shown to users.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the connector's name, shown.
    /// </summary>
    [BindNever]
    public string ConnectorDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the connector's description, shown.
    /// </summary>
    [BindNever]
    public string ConnectorDescription { get; set; }
}

/// <summary>
/// The settings of an HTTP-based connection.
/// </summary>
public class HttpConnectionViewModel
{
    /// <summary>
    /// Gets or sets the base URL.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the seconds a request may take.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 100;

    /// <summary>
    /// Gets or sets extra headers, one <c>Name: value</c> per line.
    /// </summary>
    public string Headers { get; set; }

    /// <summary>
    /// Gets or sets the kind of authentication.
    /// </summary>
    public HttpAuthenticationType AuthenticationType { get; set; }

    /// <summary>
    /// Gets or sets the API key header or parameter name.
    /// </summary>
    public string ApiKeyName { get; set; }

    /// <summary>
    /// Gets or sets where the API key is sent.
    /// </summary>
    public HttpApiKeyLocation ApiKeyLocation { get; set; }

    /// <summary>
    /// Gets or sets the text before the API key.
    /// </summary>
    public string ApiKeyPrefix { get; set; }

    /// <summary>
    /// Gets or sets a new API key; empty keeps the stored one.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the user name.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets a new password; empty keeps the stored one.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets a new bearer token; empty keeps the stored one.
    /// </summary>
    public string BearerToken { get; set; }

    /// <summary>
    /// Gets or sets the OAuth 2.0 token endpoint.
    /// </summary>
    public string TokenUrl { get; set; }

    /// <summary>
    /// Gets or sets the OpenID Connect issuer.
    /// </summary>
    public string Authority { get; set; }

    /// <summary>
    /// Gets or sets the client identifier.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets a new client secret; empty keeps the stored one.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the scopes.
    /// </summary>
    public string Scope { get; set; }

    /// <summary>
    /// Gets or sets the audience.
    /// </summary>
    public string Audience { get; set; }

    /// <summary>
    /// Gets or sets the grant.
    /// </summary>
    public OAuthGrantType GrantType { get; set; }

    /// <summary>
    /// Gets or sets how the client authenticates to the token endpoint.
    /// </summary>
    public OAuthClientAuthentication ClientAuthentication { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an API key is stored.
    /// </summary>
    [BindNever]
    public bool HasApiKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a password is stored.
    /// </summary>
    [BindNever]
    public bool HasPassword { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a bearer token is stored.
    /// </summary>
    [BindNever]
    public bool HasBearerToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a client secret is stored.
    /// </summary>
    [BindNever]
    public bool HasClientSecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the connection is to Elasticsearch, which offers fewer kinds of
    /// authentication.
    /// </summary>
    [BindNever]
    public bool IsElasticsearch { get; set; }

    /// <summary>
    /// Gets or sets the kinds of authentication offered.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AuthenticationTypes { get; set; } = [];
}

/// <summary>
/// The settings of a media files connection.
/// </summary>
public class MediaFilesConnectionViewModel
{
    /// <summary>
    /// Gets or sets the media library folder.
    /// </summary>
    public string Folder { get; set; }
}

/// <summary>
/// The fields every connected data set has.
/// </summary>
public class ConnectedDataSetFieldsViewModel
{
    /// <summary>
    /// Gets or sets the name shown to users.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the technical name consumers store.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the resource read.
    /// </summary>
    public string Resource { get; set; }

    /// <summary>
    /// Gets or sets how consumers read the data set.
    /// </summary>
    public DataSetAccessMode AccessMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the data set is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the fields.
    /// </summary>
    public List<ConnectedDataFieldViewModel> Fields { get; set; } = [];

    /// <summary>
    /// Gets or sets the default date field.
    /// </summary>
    public string DefaultDateField { get; set; }

    /// <summary>
    /// Gets or sets the minutes between scheduled refreshes.
    /// </summary>
    public int? RefreshIntervalMinutes { get; set; }

    /// <summary>
    /// Gets or sets the refresh mode.
    /// </summary>
    public DataSetRefreshMode RefreshMode { get; set; }

    /// <summary>
    /// Gets or sets the incremental field.
    /// </summary>
    public string IncrementalField { get; set; }

    /// <summary>
    /// Gets or sets what a refresh does when the source's fields change.
    /// </summary>
    public DataSetSchemaChangePolicy SchemaChangePolicy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the data set is new, so its name can still change.
    /// </summary>
    [BindNever]
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the connection's name, shown.
    /// </summary>
    [BindNever]
    public string ConnectionDisplayText { get; set; }

    /// <summary>
    /// Gets or sets the connector's name, shown.
    /// </summary>
    [BindNever]
    public string ConnectorDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the resources the connection lists.
    /// </summary>
    [BindNever]
    public IReadOnlyList<DataConnectorResource> Resources { get; set; } = [];

    /// <summary>
    /// Gets or sets why the resources couldn't be listed.
    /// </summary>
    [BindNever]
    public string DiscoveryError { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the connector reads live.
    /// </summary>
    [BindNever]
    public bool SupportsLive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the connector imports.
    /// </summary>
    [BindNever]
    public bool SupportsImport { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the connector imports incrementally.
    /// </summary>
    [BindNever]
    public bool SupportsIncremental { get; set; }

    /// <summary>
    /// Gets or sets what the last refreshes left.
    /// </summary>
    [BindNever]
    public DataSetImportState Import { get; set; }

    /// <summary>
    /// Gets or sets the permission that reads the data set.
    /// </summary>
    [BindNever]
    public string PermissionName { get; set; }

    /// <summary>
    /// Gets or sets the field types offered.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> FieldTypes { get; set; } = [];
}

/// <summary>
/// One field of a connected data set in its editor.
/// </summary>
public class ConnectedDataFieldViewModel
{
    /// <summary>
    /// Gets or sets the field's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the source field's name.
    /// </summary>
    public string SourceName { get; set; }

    /// <summary>
    /// Gets or sets the name shown to users.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the type values are read as.
    /// </summary>
    public DataFieldType Type { get; set; }

    /// <summary>
    /// Gets or sets the type the source describes.
    /// </summary>
    public DataFieldType SourceType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the field identifies a row.
    /// </summary>
    public bool IsIdentifier { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether consumers see the field.
    /// </summary>
    public bool IsIncluded { get; set; }
}

/// <summary>
/// The settings of a data set that reads an HTTP API.
/// </summary>
public class HttpApiDataSetViewModel
{
    /// <summary>
    /// Gets or sets the HTTP method.
    /// </summary>
    public string Method { get; set; } = "GET";

    /// <summary>
    /// Gets or sets the body of a POST.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the records path.
    /// </summary>
    public string RecordsPath { get; set; }

    /// <summary>
    /// Gets or sets the file format of the response, or empty for JSON.
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets the paging strategy.
    /// </summary>
    public HttpPaginationType PaginationType { get; set; }

    /// <summary>
    /// Gets or sets the page number parameter.
    /// </summary>
    public string PageParameter { get; set; }

    /// <summary>
    /// Gets or sets the first page's number.
    /// </summary>
    public int FirstPage { get; set; } = 1;

    /// <summary>
    /// Gets or sets the offset parameter.
    /// </summary>
    public string OffsetParameter { get; set; }

    /// <summary>
    /// Gets or sets the page size parameter.
    /// </summary>
    public string SizeParameter { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the cursor parameter.
    /// </summary>
    public string CursorParameter { get; set; }

    /// <summary>
    /// Gets or sets the path of the next cursor or link.
    /// </summary>
    public string NextPath { get; set; }

    /// <summary>
    /// Gets or sets the most pages read.
    /// </summary>
    public int MaxPages { get; set; } = 1_000;

    /// <summary>
    /// Gets or sets the file formats offered.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Formats { get; set; } = [];
}

/// <summary>
/// The settings of a data set that reads an Elasticsearch index.
/// </summary>
public class ElasticsearchDataSetViewModel
{
    /// <summary>
    /// Gets or sets the query clause.
    /// </summary>
    public string Query { get; set; }

    /// <summary>
    /// Gets or sets the scroll keep-alive in minutes.
    /// </summary>
    public int ScrollMinutes { get; set; } = 2;
}

/// <summary>
/// The settings of a data set that reads a relational database.
/// </summary>
public class RelationalDataSetViewModel
{
    /// <summary>
    /// Gets or sets the custom query.
    /// </summary>
    public string Query { get; set; }

    /// <summary>
    /// Gets or sets the seconds a query may run.
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 300;
}

/// <summary>
/// The settings of a data set that reads a media library file.
/// </summary>
public class MediaFileDataSetViewModel
{
    /// <summary>
    /// Gets or sets the CSV delimiter.
    /// </summary>
    public string Delimiter { get; set; } = ",";

    /// <summary>
    /// Gets or sets a value indicating whether the first CSV row names the columns.
    /// </summary>
    public bool HasHeaderRow { get; set; } = true;

    /// <summary>
    /// Gets or sets the Excel sheet.
    /// </summary>
    public string SheetName { get; set; }
}

/// <summary>
/// The first rows of a data set, as consumers read them.
/// </summary>
public class DataSetPreviewViewModel
{
    /// <summary>
    /// Gets or sets the data set.
    /// </summary>
    public ConnectedDataSet DataSet { get; set; }

    /// <summary>
    /// Gets or sets the data source read.
    /// </summary>
    public string SourceName { get; set; }

    /// <summary>
    /// Gets or sets the fields.
    /// </summary>
    public IReadOnlyList<DataField> Fields { get; set; } = [];

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    public IList<object[]> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the data set has more rows.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>
    /// Gets or sets why the rows couldn't be read.
    /// </summary>
    public string Error { get; set; }
}

/// <summary>
/// The refresh history of a data set.
/// </summary>
public class DataSetRunsViewModel
{
    /// <summary>
    /// Gets or sets the data set.
    /// </summary>
    public ConnectedDataSet DataSet { get; set; }

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    public AdminListContext List { get; set; }
}

/// <summary>
/// The row of a connected data set with its connection.
/// </summary>
public class ConnectedDataSetRowViewModel
{
    /// <summary>
    /// Gets or sets the data set.
    /// </summary>
    public ConnectedDataSet DataSet { get; set; }

    /// <summary>
    /// Gets or sets the data set's connection, or <see langword="null"/> when it is gone.
    /// </summary>
    public DataConnection Connection { get; set; }
}

/// <summary>
/// The editor of a deployment step that exports connections or data sets.
/// </summary>
public class DataConnectorDeploymentStepViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether every record is exported.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the records to choose from.
    /// </summary>
    public DataConnectorDeploymentEntryViewModel[] Entries { get; set; } = [];
}

/// <summary>
/// A record a deployment step can export.
/// </summary>
public class DataConnectorDeploymentEntryViewModel
{
    /// <summary>
    /// Gets or sets the record's identifier.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the record's name.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the record is exported.
    /// </summary>
    public bool IsSelected { get; set; }
}
