using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CrestApps.Core;
using CrestApps.OrchardCore.DataConnectors.Http;
using Microsoft.AspNetCore.WebUtilities;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Files;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.DataConnectors.Connectors.Http;

/// <summary>
/// Reads any HTTP API that answers with JSON, or with a file format such as CSV: a data set's resource is a path on the
/// connection's base URL, its records are found at a path of the response, and its pages are followed by page number,
/// offset, cursor, next link or <c>Link</c> header.
/// </summary>
internal sealed partial class HttpApiDataConnector : IDataConnector
{
    private const int _sampleRecords = 200;

    private readonly DataConnectorHttpClient _httpClient;
    private readonly HttpRequestAuthenticator _authenticator;
    private readonly IDataFileFormatManager _fileFormatManager;

    public HttpApiDataConnector(
        DataConnectorHttpClient httpClient,
        HttpRequestAuthenticator authenticator,
        IDataFileFormatManager fileFormatManager)
    {
        _httpClient = httpClient;
        _authenticator = authenticator;
        _fileFormatManager = fileFormatManager;
    }

    public static string Name => "HttpApi";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<HttpApiDataConnector>("HTTP API");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<HttpApiDataConnector>("Reads any HTTP or REST API that answers with JSON, CSV or JSON Lines, with API key, Basic, bearer token, OAuth 2.0 or OpenID Connect authentication, and follows its pages.");

    public static DataConnectorCapabilities Capabilities => DataConnectorCapabilities.Live | DataConnectorCapabilities.Import;

    public async Task<DataConnectorTestResult> TestAsync(DataConnectorContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = context.Connection.GetOrCreate<HttpConnectionSettings>();

        try
        {
            var baseUri = DataConnectorHttpClient.GetBaseUri(settings);
            var tokenMessage = string.Empty;

            if (HttpRequestAuthenticator.UsesTokens(settings.Authentication))
            {
                await _authenticator.RequestTokenAsync(settings.Authentication, context.Secrets, cancellationToken);
                tokenMessage = "A token was issued. ";
            }

            using var response = await _httpClient.SendAsync(settings, context.Secrets, DataConnectorHttpClient.CacheKey(context.Connection), HttpMethod.Get, baseUri, null, cancellationToken, ensureSuccess: false);
            var status = (int)response.StatusCode;

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return DataConnectorTestResult.Failure($"{tokenMessage}The server refused the credentials ({status} {response.ReasonPhrase}).");
            }

            return DataConnectorTestResult.Success($"{tokenMessage}The server answered {status} {response.ReasonPhrase} at {DataConnectorHttpClient.Describe(baseUri)}.");
        }
        catch (DataConnectorException exception)
        {
            return DataConnectorTestResult.Failure(exception.Message);
        }
    }

    public Task<IReadOnlyList<DataConnectorResource>> DiscoverAsync(DataConnectorContext context, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DataConnectorResource>>([]);

    public async Task<IReadOnlyList<DataField>> DescribeAsync(DataConnectorReadContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var dataSetSettings = context.DataSet.GetOrCreate<HttpApiDataSetSettings>();

        if (GetFormat(dataSetSettings) is { } format)
        {
            IReadOnlyList<DataField> fields = [];
            var rows = new List<object[]>();

            await foreach (var batch in ReadFileAsync(context, format, cancellationToken))
            {
                fields = batch.Fields;
                rows.AddRange(batch.Rows.Take(_sampleRecords - rows.Count));

                if (rows.Count >= _sampleRecords)
                {
                    break;
                }
            }

            return FieldInference.Infer([.. fields.Select(field => field.Name)], rows, parseText: true);
        }

        var records = new List<Dictionary<string, object>>();

        await foreach (var page in ReadPagesAsync(context, dataSetSettings, maxPages: 2, cancellationToken))
        {
            records.AddRange(page.Select(JsonRecords.Flatten).Take(_sampleRecords - records.Count));

            if (records.Count >= _sampleRecords)
            {
                break;
            }
        }

        return FieldInference.Infer(records);
    }

    public async IAsyncEnumerable<DataBatch> ReadAsync(DataConnectorReadContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var dataSetSettings = context.DataSet.GetOrCreate<HttpApiDataSetSettings>();

        if (GetFormat(dataSetSettings) is { } format)
        {
            await foreach (var batch in ReadFileAsync(context, format, cancellationToken))
            {
                yield return batch;
            }

            yield break;
        }

        IReadOnlyList<DataField> fields = null;

        if (context.Fields.Count > 0)
        {
            fields = [.. context.Fields.Select(name => new DataField(name, name, DataFieldType.Text))];
        }

        var any = false;

        await foreach (var page in ReadPagesAsync(context, dataSetSettings, maxPages: 0, cancellationToken))
        {
            var flattened = page.Select(JsonRecords.Flatten).ToArray();

            fields ??= FieldInference.Infer(flattened);

            var rows = flattened
                .Select(record => fields.Select(field => record.GetValueOrDefault(field.Name)).ToArray())
                .ToArray();

            any = true;

            yield return new DataBatch(fields, rows);
        }

        if (!any)
        {
            yield return new DataBatch(fields ?? [], []);
        }
    }

    /// <summary>
    /// Reads the record pages of a data set, following its paging strategy.
    /// </summary>
    internal async IAsyncEnumerable<IReadOnlyList<JsonElement>> ReadPagesAsync(
        DataConnectorReadContext context,
        HttpApiDataSetSettings dataSetSettings,
        int maxPages,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var settings = context.Connection.GetOrCreate<HttpConnectionSettings>();
        var pagination = dataSetSettings.Pagination ?? new HttpPaginationSettings();
        var pageSize = pagination.PageSize > 0 ? pagination.PageSize : 100;
        var pageLimit = pagination.MaxPages > 0 ? pagination.MaxPages : 1_000;

        if (maxPages > 0)
        {
            pageLimit = Math.Min(pageLimit, maxPages);
        }

        var method = string.Equals(dataSetSettings.Method, "POST", StringComparison.OrdinalIgnoreCase) ? HttpMethod.Post : HttpMethod.Get;
        var start = DataConnectorHttpClient.Resolve(settings, Render(context.DataSet.Resource, context.Parameters, Uri.EscapeDataString));
        var cacheKey = DataConnectorHttpClient.CacheKey(context.Connection);
        var next = start;
        string cursor = null;
        long read = 0;

        for (var page = 0; page < pageLimit && next is not null; page++)
        {
            var uri = pagination.Type switch
            {
                HttpPaginationType.PageNumber => WithPaging(start, pagination, pagination.PageParameter, pagination.FirstPage + page, pageSize),
                HttpPaginationType.Offset => WithPaging(start, pagination, pagination.OffsetParameter, (long)page * pageSize, pageSize),
                HttpPaginationType.Cursor => page == 0
                    ? WithPaging(start, pagination, null, null, pageSize)
                    : WithPaging(start, pagination, pagination.CursorParameter, cursor, pageSize),
                _ => next,
            };

            var values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["page"] = (pagination.FirstPage + page).ToString(CultureInfo.InvariantCulture),
                ["offset"] = ((long)page * pageSize).ToString(CultureInfo.InvariantCulture),
                ["limit"] = pageSize.ToString(CultureInfo.InvariantCulture),
                ["cursor"] = cursor ?? string.Empty,
            };

            Func<HttpContent> body = method == HttpMethod.Post
                ? () => new StringContent(RenderBody(dataSetSettings.Body, values, context.Parameters), Encoding.UTF8, "application/json")
                : null;

            using var response = await _httpClient.SendAsync(settings, context.Secrets, cacheKey, method, uri, body, cancellationToken);
            using var document = await _httpClient.ReadJsonAsync(response, cancellationToken);

            var records = JsonRecords.SelectRecords(document.RootElement, dataSetSettings.RecordsPath);

            if (records.Count == 0)
            {
                yield break;
            }

            // The records belong to the document, which is disposed when the next page is read.
            yield return [.. records.Select(record => record.Clone())];

            read += records.Count;

            if (context.MaxRows > 0 && read >= context.MaxRows)
            {
                yield break;
            }

            next = null;

            switch (pagination.Type)
            {
                case HttpPaginationType.PageNumber:
                case HttpPaginationType.Offset:
                    // When the API is told the page size, a short page is the last one; otherwise an empty page is.
                    if (string.IsNullOrWhiteSpace(pagination.SizeParameter) || records.Count >= pageSize)
                    {
                        next = start;
                    }

                    break;

                case HttpPaginationType.Cursor:
                    cursor = JsonRecords.SelectText(document.RootElement, pagination.NextPath);
                    next = string.IsNullOrEmpty(cursor) ? null : start;
                    break;

                case HttpPaginationType.NextLink:
                    var link = JsonRecords.SelectText(document.RootElement, pagination.NextPath);
                    next = string.IsNullOrWhiteSpace(link) ? null : DataConnectorHttpClient.Resolve(settings, new Uri(uri, link).AbsoluteUri);
                    break;

                case HttpPaginationType.LinkHeader:
                    var header = NextLink(response);
                    next = header is null ? null : DataConnectorHttpClient.Resolve(settings, new Uri(uri, header).AbsoluteUri);
                    break;
            }
        }
    }

    /// <summary>
    /// Returns the <c>rel="next"</c> URL of a response's <c>Link</c> header, or <see langword="null"/>.
    /// </summary>
    internal static string NextLink(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var values))
        {
            return null;
        }

        foreach (var value in values)
        {
            foreach (Match match in LinkHeaderPattern().Matches(value))
            {
                if (match.Groups["rel"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("next", StringComparer.OrdinalIgnoreCase))
                {
                    return match.Groups["url"].Value;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Replaces the <c>{param:Name}</c> placeholders of a template with the read's parameters.
    /// </summary>
    internal static string Render(string template, IDictionary<string, object> parameters, Func<string, string> encode)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        return ParameterPattern().Replace(template, match =>
        {
            var name = match.Groups["name"].Value;

            return parameters is not null && parameters.TryGetValue(name, out var value) && value is not null
                ? encode(DataValues.ToText(value) ?? string.Empty)
                : string.Empty;
        });
    }

    private static string RenderBody(string template, Dictionary<string, string> paging, IDictionary<string, object> parameters)
    {
        var body = Render(string.IsNullOrWhiteSpace(template) ? "{}" : template, parameters, value => JsonEncodedText.Encode(value).ToString());

        foreach (var (name, value) in paging)
        {
            body = body.Replace("{" + name + "}", JsonEncodedText.Encode(value).ToString(), StringComparison.Ordinal);
        }

        return body;
    }

    private static Uri WithPaging(Uri start, HttpPaginationSettings pagination, string parameter, object value, int pageSize)
    {
        var url = start.AbsoluteUri;

        if (!string.IsNullOrWhiteSpace(parameter) && value is not null)
        {
            url = QueryHelpers.AddQueryString(url, parameter.Trim(), Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(pagination.SizeParameter))
        {
            url = QueryHelpers.AddQueryString(url, pagination.SizeParameter.Trim(), pageSize.ToString(CultureInfo.InvariantCulture));
        }

        return new Uri(url);
    }

    private IDataFileFormat GetFormat(HttpApiDataSetSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Format) || string.Equals(settings.Format, "json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return _fileFormatManager.GetFormat(settings.Format.Trim())
            ?? throw new DataConnectorException($"The file format '{settings.Format}' isn't registered.");
    }

    private async IAsyncEnumerable<DataBatch> ReadFileAsync(DataConnectorReadContext context, IDataFileFormat format, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var settings = context.Connection.GetOrCreate<HttpConnectionSettings>();
        var uri = DataConnectorHttpClient.Resolve(settings, Render(context.DataSet.Resource, context.Parameters, Uri.EscapeDataString));

        using var response = await _httpClient.SendAsync(settings, context.Secrets, DataConnectorHttpClient.CacheKey(context.Connection), HttpMethod.Get, uri, null, cancellationToken);
        await using var stream = await _httpClient.ReadStreamAsync(response, cancellationToken);

        var options = new DataFileOptions
        {
            BatchSize = context.BatchSize > 0 ? context.BatchSize : DataSourceQuery.DefaultBatchSize,
        };

        await foreach (var batch in format.ReadAsync(stream, options, cancellationToken))
        {
            yield return batch;
        }
    }

    [GeneratedRegex(@"\{param:(?<name>[A-Za-z0-9_\-\.]+)\}")]
    private static partial Regex ParameterPattern();

    [GeneratedRegex(@"<(?<url>[^>]+)>\s*;[^,]*?rel=""?(?<rel>[^"";,]+)""?")]
    private static partial Regex LinkHeaderPattern();
}
