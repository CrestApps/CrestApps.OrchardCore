using System.Diagnostics;
using System.Runtime.CompilerServices;
using CrestApps.Core;
using CrestApps.OrchardCore.DataConnectors.Telemetry;
using CrestApps.OrchardCore.Diagnostics;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Files;
using OrchardCore.FileStorage;
using OrchardCore.Localization;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.DataConnectors.Connectors.Files;

/// <summary>
/// The folder of the media library a files connection reads.
/// </summary>
public sealed class MediaFilesConnectionSettings
{
    /// <summary>
    /// Gets or sets the folder of the media library, or <see langword="null"/> for the whole library.
    /// </summary>
    public string Folder { get; set; }
}

/// <summary>
/// How a data set reads its file. The file's path in the media library is the data set's resource.
/// </summary>
public sealed class MediaFileDataSetSettings
{
    /// <summary>
    /// Gets or sets the delimiter of a CSV file.
    /// </summary>
    public string Delimiter { get; set; } = ",";

    /// <summary>
    /// Gets or sets a value indicating whether a CSV file's first row names its columns.
    /// </summary>
    public bool HasHeaderRow { get; set; } = true;

    /// <summary>
    /// Gets or sets the sheet of an Excel workbook read, or <see langword="null"/> for the first.
    /// </summary>
    public string SheetName { get; set; }
}

/// <summary>
/// Reads structured files of the media library, such as CSV, JSON, JSON Lines and Excel files, through the file formats
/// the data sources register.
/// </summary>
internal sealed class MediaFilesDataConnector : IDataConnector
{
    private const int _sampleRows = 200;

    private readonly IMediaFileStore _mediaFileStore;
    private readonly IDataFileFormatManager _fileFormatManager;

    public MediaFilesDataConnector(
        IMediaFileStore mediaFileStore,
        IDataFileFormatManager fileFormatManager)
    {
        _mediaFileStore = mediaFileStore;
        _fileFormatManager = fileFormatManager;
    }

    public static string Name => DataConnectorConstants.Connectors.MediaFiles;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<MediaFilesDataConnector>("Media library files");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<MediaFilesDataConnector>("Reads CSV, JSON, JSON Lines and Excel files uploaded to the media library.");

    public static DataConnectorCapabilities Capabilities =>
        DataConnectorCapabilities.Live |
        DataConnectorCapabilities.Import |
        DataConnectorCapabilities.Discovery;

    public async Task<DataConnectorTestResult> TestAsync(DataConnectorContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var folder = GetFolder(context);

        if (!string.IsNullOrEmpty(folder) && await _mediaFileStore.GetDirectoryInfoAsync(folder) is null)
        {
            return DataConnectorTestResult.Failure($"The media library has no folder '{folder}'.");
        }

        var files = await DiscoverAsync(context, cancellationToken);

        return DataConnectorTestResult.Success($"Found {files.Count} readable file(s).");
    }

    public async Task<IReadOnlyList<DataConnectorResource>> DiscoverAsync(DataConnectorContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var folder = GetFolder(context);
        var resources = new List<DataConnectorResource>();

        await foreach (var entry in _mediaFileStore.GetDirectoryContentAsync(string.IsNullOrEmpty(folder) ? null : folder, includeSubDirectories: true).WithCancellation(cancellationToken))
        {
            if (!entry.IsDirectory && _fileFormatManager.GetDescriptorByFileName(entry.Name) is { } format)
            {
                resources.Add(new DataConnectorResource(entry.Path, entry.Path, format.DisplayName.Value));
            }

            if (resources.Count >= 1_000)
            {
                break;
            }
        }

        return [.. resources.OrderBy(resource => resource.Name, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<IReadOnlyList<DataField>> DescribeAsync(DataConnectorReadContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<DataField> fields = [];
        var rows = new List<object[]>();

        await foreach (var batch in ReadAsync(context, cancellationToken))
        {
            fields = batch.Fields;
            rows.AddRange(batch.Rows.Take(_sampleRows - rows.Count));

            if (rows.Count >= _sampleRows)
            {
                break;
            }
        }

        return FieldInference.Infer([.. fields.Select(field => field.Name)], rows, parseText: true);
    }

    public async IAsyncEnumerable<DataBatch> ReadAsync(DataConnectorReadContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var path = GetFilePath(context);
        var descriptor = _fileFormatManager.GetDescriptorByFileName(path)
            ?? throw new DataConnectorException($"No file format reads '{Path.GetFileName(path)}'.");
        var format = _fileFormatManager.GetFormat(descriptor.Name);

        if (await _mediaFileStore.GetFileInfoAsync(path) is null)
        {
            throw new DataConnectorException($"The media library has no file '{path}'.");
        }

        var settings = context.DataSet.GetOrCreate<MediaFileDataSetSettings>();
        var options = new DataFileOptions
        {
            Delimiter = string.IsNullOrEmpty(settings.Delimiter) ? "," : settings.Delimiter,
            HasHeaderRow = settings.HasHeaderRow,
            SheetName = string.IsNullOrWhiteSpace(settings.SheetName) ? null : settings.SheetName.Trim(),
            BatchSize = context.BatchSize > 0 ? context.BatchSize : DataSourceQuery.DefaultBatchSize,
        };

        await using var stream = await _mediaFileStore.GetFileStreamAsync(path);
        await using var batches = format.ReadAsync(stream, options, cancellationToken).GetAsyncEnumerator(cancellationToken);

        // Only the time the format spends producing rows is counted, not the time the consumer spends between batches.
        var parsing = TimeSpan.Zero;
        var outcome = CrestAppsOrchardCoreTelemetry.OutcomeOk;

        try
        {
            while (true)
            {
                var startTimestamp = Stopwatch.GetTimestamp();
                bool hasBatch;

                try
                {
                    hasBatch = await batches.MoveNextAsync();
                }
                catch (Exception exception)
                {
                    outcome = CrestAppsOrchardCoreTelemetry.GetOutcome(exception);

                    throw;
                }
                finally
                {
                    parsing += Stopwatch.GetElapsedTime(startTimestamp);
                }

                if (!hasBatch)
                {
                    break;
                }

                yield return batches.Current;
            }
        }
        finally
        {
            DataConnectorsTelemetry.RecordDuration(DataConnectorsTelemetry.FileParseDuration, parsing, outcome, new TagList
            {
                { CrestAppsOrchardCoreTelemetry.FormatTag, descriptor.Name },
            });
        }
    }

    private string GetFolder(DataConnectorContext context)
    {
        var folder = context.Connection.GetOrCreate<MediaFilesConnectionSettings>().Folder;

        return string.IsNullOrWhiteSpace(folder) ? null : Normalize(folder);
    }

    // The path must stay inside the connection's folder, whatever the data set's resource says.
    private string GetFilePath(DataConnectorReadContext context)
    {
        if (string.IsNullOrWhiteSpace(context.DataSet.Resource))
        {
            throw new DataConnectorException("The data set names no file.");
        }

        var path = Normalize(context.DataSet.Resource);
        var folder = GetFolder(context);

        if (!string.IsNullOrEmpty(folder) && !path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase))
        {
            path = _mediaFileStore.Combine(folder, path);
        }

        return path;
    }

    private string Normalize(string path)
    {
        var normalized = _mediaFileStore.NormalizePath(path.Trim().Replace('\\', '/')).Trim('/');

        if (normalized.Split('/').Any(segment => segment is ".." or "."))
        {
            throw new DataConnectorException("A media path can't contain '.' or '..' segments.");
        }

        return normalized;
    }
}
