namespace CrestApps.OrchardCore.DataConnectors.Connectors.Http;

/// <summary>
/// How a data set reads an HTTP API: the method and body of the request, where the records are in the response,
/// and how the next page is asked for. The path is the data set's resource.
/// </summary>
public sealed class HttpApiDataSetSettings
{
    /// <summary>
    /// Gets or sets the HTTP method: <c>GET</c> or <c>POST</c>.
    /// </summary>
    public string Method { get; set; } = "GET";

    /// <summary>
    /// Gets or sets the JSON body of a <c>POST</c>. <c>{page}</c>, <c>{offset}</c>, <c>{limit}</c> and
    /// <c>{cursor}</c> are replaced by the paging values, and <c>{param:Name}</c> by a read parameter.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the dotted path of the records in the response, such as <c>data.items</c>; empty for the root.
    /// </summary>
    public string RecordsPath { get; set; }

    /// <summary>
    /// Gets or sets the name of the file format the response is in, such as <c>csv</c>, or <see langword="null"/>
    /// for JSON.
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets how the next page is asked for.
    /// </summary>
    public HttpPaginationSettings Pagination { get; set; } = new();
}

/// <summary>
/// How an HTTP API is paged.
/// </summary>
public sealed class HttpPaginationSettings
{
    /// <summary>
    /// Gets or sets the paging strategy.
    /// </summary>
    public HttpPaginationType Type { get; set; }

    /// <summary>
    /// Gets or sets the query parameter of the page number.
    /// </summary>
    public string PageParameter { get; set; } = "page";

    /// <summary>
    /// Gets or sets the number of the first page.
    /// </summary>
    public int FirstPage { get; set; } = 1;

    /// <summary>
    /// Gets or sets the query parameter of the offset.
    /// </summary>
    public string OffsetParameter { get; set; } = "offset";

    /// <summary>
    /// Gets or sets the query parameter of the page size, or <see langword="null"/> to send none.
    /// </summary>
    public string SizeParameter { get; set; } = "limit";

    /// <summary>
    /// Gets or sets the number of records asked for per page.
    /// </summary>
    public int PageSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the query parameter of the cursor.
    /// </summary>
    public string CursorParameter { get; set; } = "cursor";

    /// <summary>
    /// Gets or sets the dotted path of the next cursor, or of the next page's URL, in the response.
    /// </summary>
    public string NextPath { get; set; }

    /// <summary>
    /// Gets or sets the most pages one read asks for.
    /// </summary>
    public int MaxPages { get; set; } = 1_000;
}

/// <summary>
/// The paging strategies of HTTP APIs.
/// </summary>
public enum HttpPaginationType
{
    /// <summary>
    /// One request returns every record.
    /// </summary>
    None,

    /// <summary>
    /// A page number in the query string, from the first page on.
    /// </summary>
    PageNumber,

    /// <summary>
    /// An offset in the query string, from zero on.
    /// </summary>
    Offset,

    /// <summary>
    /// A cursor the response returns, sent back in the query string.
    /// </summary>
    Cursor,

    /// <summary>
    /// The URL of the next page the response returns at a path.
    /// </summary>
    NextLink,

    /// <summary>
    /// The URL of the next page in the response's <c>Link</c> header, with <c>rel="next"</c>.
    /// </summary>
    LinkHeader,
}
