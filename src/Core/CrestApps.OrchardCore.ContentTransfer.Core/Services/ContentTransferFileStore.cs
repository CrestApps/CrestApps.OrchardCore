using System.Diagnostics;
using CrestApps.OrchardCore.ContentTransfer.Telemetry;
using CrestApps.OrchardCore.Diagnostics;
using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.ContentTransfer.Services;

public sealed class ContentTransferFileStore : IContentTransferFileStore
{
    private readonly IFileStore _fileStore;
    private readonly string _backend;

    public ContentTransferFileStore(IFileStore fileStore)
    {
        _fileStore = fileStore;
        _backend = ContentTransferDiagnostics.GetBackend(fileStore);
    }

    public IFileStoreCapabilities Capabilities
        => _fileStore.Capabilities;

    public Task CopyFileAsync(string srcPath, string dstPath)
        => MeasureAsync("copy", () => _fileStore.CopyFileAsync(srcPath, dstPath));

    public Task<string> CreateFileFromStreamAsync(string path, Stream inputStream, bool overwrite = false)
        => MeasureAsync("create", () => _fileStore.CreateFileFromStreamAsync(path, inputStream, overwrite));

    public IAsyncEnumerable<IFileStoreEntry> GetDirectoryContentAsync(string path = null, bool includeSubDirectories = false)
        => _fileStore.GetDirectoryContentAsync(path, includeSubDirectories);

    public Task<IFileStoreEntry> GetDirectoryInfoAsync(string path)
        => _fileStore.GetDirectoryInfoAsync(path);

    public Task<IFileStoreEntry> GetFileInfoAsync(string path)
        => MeasureAsync("get_info", () => _fileStore.GetFileInfoAsync(path));

    public Task<Stream> GetFileStreamAsync(string path)
        => MeasureAsync("open_read", () => _fileStore.GetFileStreamAsync(path));

    public Task<Stream> GetFileStreamAsync(IFileStoreEntry fileStoreEntry)
        => MeasureAsync("open_read", () => _fileStore.GetFileStreamAsync(fileStoreEntry));

    public Task MoveFileAsync(string oldPath, string newPath)
        => MeasureAsync("move", () => _fileStore.MoveFileAsync(oldPath, newPath));

    public Task<bool> TryCreateDirectoryAsync(string path)
        => _fileStore.TryCreateDirectoryAsync(path);

    public Task<bool> TryDeleteDirectoryAsync(string path)
        => _fileStore.TryDeleteDirectoryAsync(path);

    public Task<bool> TryDeleteFileAsync(string path)
        => MeasureAsync("delete", () => _fileStore.TryDeleteFileAsync(path));

    private Task MeasureAsync(string operation, Func<Task> work)
    {
        if (!ContentTransferDiagnostics.StoreDuration.Enabled)
        {
            return work();
        }

        return MeasureCoreAsync(operation, async () =>
        {
            await work();

            return true;
        });
    }

    private Task<T> MeasureAsync<T>(string operation, Func<Task<T>> work)
    {
        if (!ContentTransferDiagnostics.StoreDuration.Enabled)
        {
            return work();
        }

        return MeasureCoreAsync(operation, work);
    }

    private async Task<T> MeasureCoreAsync<T>(string operation, Func<Task<T>> work)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var outcome = CrestAppsOrchardCoreTelemetry.OutcomeError;

        try
        {
            var result = await work();
            outcome = CrestAppsOrchardCoreTelemetry.OutcomeOk;

            return result;
        }
        catch (Exception ex)
        {
            outcome = CrestAppsOrchardCoreTelemetry.GetOutcome(ex);

            throw;
        }
        finally
        {
            ContentTransferDiagnostics.Record(ContentTransferDiagnostics.StoreDuration, Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, new TagList
            {
                { CrestAppsOrchardCoreTelemetry.OperationTag, operation },
                { ContentTransferDiagnostics.BackendTag, _backend },
                { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome },
            });
        }
    }
}
