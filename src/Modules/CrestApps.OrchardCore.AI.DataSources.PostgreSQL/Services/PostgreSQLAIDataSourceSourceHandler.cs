using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Services;

internal sealed class PostgreSQLAIDataSourceSourceHandler : IAIDataSourceSourceHandler
{
    private const int BatchSize = 1000;

    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<PostgreSQLAIDataSourceSourceHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSQLAIDataSourceSourceHandler"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="logger">The logger.</param>
    public PostgreSQLAIDataSourceSourceHandler(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<PostgreSQLAIDataSourceSourceHandler> logger)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    public string SourceType => AIDataSourceSourceTypes.PostgreSQL;

    public ValueTask ValidateAsync(
        AIDataSource dataSource,
        ValidationResultDetails result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(result);

        if (!dataSource.TryGet<PostgreSQLSourceMetadata>(out var metadata))
        {
            result.Fail(new ValidationResult("PostgreSQL source settings are required.", [nameof(PostgreSQLSourceMetadata)]));

            return ValueTask.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(metadata.ConnectionString))
        {
            result.Fail(new ValidationResult("PostgreSQL connection string is required.", [nameof(PostgreSQLSourceMetadata.ConnectionString)]));
        }

        if (string.IsNullOrWhiteSpace(metadata.TableName))
        {
            result.Fail(new ValidationResult("PostgreSQL table name is required.", [nameof(PostgreSQLSourceMetadata.TableName)]));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<string> GetReferenceTypeAsync(
        AIDataSource dataSource,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(SourceType);

    public async IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadAsync(
        AIDataSource dataSource,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var metadata = GetMetadata(dataSource);
        var connectionString = GetConnectionString(dataSource, metadata);
        var quotedTableName = QuoteCompositeIdentifier(metadata.TableName);
        var sortColumn = ResolveSortColumn(dataSource);
        var offset = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"""SELECT * FROM {quotedTableName} ORDER BY {sortColumn} LIMIT @limit OFFSET @offset""";
            command.Parameters.AddWithValue("limit", BatchSize);
            command.Parameters.AddWithValue("offset", offset);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rowCount = 0;

            while (await reader.ReadAsync(cancellationToken))
            {
                rowCount++;
                var row = ReadRow(reader);
                var key = ResolveKey(row, dataSource.KeyFieldName);

                yield return new KeyValuePair<string, SourceDocument>(
                    key,
                    ExtractDocument(row, dataSource.TitleFieldName, dataSource.ContentFieldName));
            }

            if (rowCount < BatchSize)
            {
                yield break;
            }

            offset += BatchSize;
        }
    }

    public async IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadByIdsAsync(
        AIDataSource dataSource,
        IEnumerable<string> documentIds,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var ids = documentIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];

        if (ids.Length == 0)
        {
            yield break;
        }

        var metadata = GetMetadata(dataSource);
        var connectionString = GetConnectionString(dataSource, metadata);
        var quotedTableName = QuoteCompositeIdentifier(metadata.TableName);
        var keyColumn = ResolveKeyColumn(dataSource);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var parameterNames = new List<string>();

        for (var i = 0; i < ids.Length; i++)
        {
            var parameterName = $"@id{i}";
            parameterNames.Add(parameterName);
            command.Parameters.AddWithValue(parameterName, ids[i]);
        }

        command.CommandText = $"""SELECT * FROM {quotedTableName} WHERE {keyColumn} IN ({string.Join(", ", parameterNames)})""";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = ReadRow(reader);
            var key = ResolveKey(row, dataSource.KeyFieldName);

            yield return new KeyValuePair<string, SourceDocument>(
                key,
                ExtractDocument(row, dataSource.TitleFieldName, dataSource.ContentFieldName));
        }
    }

    private static PostgreSQLSourceMetadata GetMetadata(AIDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        if (!dataSource.TryGet<PostgreSQLSourceMetadata>(out var metadata))
        {
            throw new InvalidOperationException("PostgreSQL source metadata is missing.");
        }

        return metadata;
    }

    private string GetConnectionString(
        AIDataSource dataSource,
        PostgreSQLSourceMetadata metadata)
    {
        var protector = _dataProtectionProvider.CreateProtector(AIDataSourceProtectionConstants.SourceSecretPurpose);

        return DataProtectionHelper.Unprotect(protector, metadata.ConnectionString, _logger, "Failed to unprotect AI data source field '{FieldName}' for data source '{DataSourceId}'.", nameof(PostgreSQLSourceMetadata.ConnectionString), dataSource.ItemId);
    }

    private static Dictionary<string, object> ReadRow(NpgsqlDataReader reader)
    {
        var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < reader.FieldCount; i++)
        {
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return row;
    }

    private static string ResolveKeyColumn(AIDataSource dataSource)
    {
        return string.IsNullOrWhiteSpace(dataSource.KeyFieldName)
            ? QuoteCompositeIdentifier("id")
            : QuoteCompositeIdentifier(dataSource.KeyFieldName);
    }

    private static string ResolveSortColumn(AIDataSource dataSource) => ResolveKeyColumn(dataSource);

    private static string ResolveKey(Dictionary<string, object> row, string keyFieldName)
    {
        if (!string.IsNullOrWhiteSpace(keyFieldName) &&
            row.TryGetValue(keyFieldName, out var configuredValue) &&
            configuredValue != null)
        {
            return configuredValue.ToString();
        }

        if (row.TryGetValue("id", out var idValue) && idValue != null)
        {
            return idValue.ToString();
        }

        return null;
    }

    private static SourceDocument ExtractDocument(
        Dictionary<string, object> row,
        string titleFieldName,
        string contentFieldName)
    {
        string title = null;
        string content = null;

        if (!string.IsNullOrWhiteSpace(titleFieldName) &&
            row.TryGetValue(titleFieldName, out var titleValue) &&
            titleValue != null)
        {
            title = titleValue.ToString();
        }

        if (!string.IsNullOrWhiteSpace(contentFieldName) &&
            row.TryGetValue(contentFieldName, out var contentValue) &&
            contentValue != null)
        {
            content = contentValue.ToString();
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            var json = new JsonObject();

            foreach (var kvp in row)
            {
                json[kvp.Key] = JsonValue.Create(kvp.Value?.ToString());
            }

            content = json.ToJsonString();
        }

        if (string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(content))
        {
            title = ExtractTitleFromContent(content);
        }

        return new SourceDocument
        {
            Title = title,
            Content = content,
            Fields = row.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase),
        };
    }

    private static string ExtractTitleFromContent(string content)
    {
        var firstLine = content.AsSpan();
        var newlineIndex = firstLine.IndexOfAny('\r', '\n');

        if (newlineIndex > 0)
        {
            firstLine = firstLine[..newlineIndex];
        }

        if (firstLine.Length > 200)
        {
            firstLine = firstLine[..200];
        }

        return firstLine.ToString().Trim();
    }

    private static string QuoteCompositeIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        return string.Join(
            ".",
            identifier
                .Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => "\"" + segment.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""));
    }
}
