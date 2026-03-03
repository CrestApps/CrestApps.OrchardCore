namespace CrestApps.Mvc.Web.Services;

public sealed class FileSystemFileStore
{
    private readonly string _basePath;

    public FileSystemFileStore(string basePath)
    {
        _basePath = basePath;
        Directory.CreateDirectory(basePath);
    }

    public async Task<string> SaveFileAsync(string fileName, Stream content)
    {
        var filePath = Path.Combine(_basePath, fileName);
        var directory = Path.GetDirectoryName(filePath);
        Directory.CreateDirectory(directory);

        using var fileStream = new FileStream(filePath, FileMode.Create);
        await content.CopyToAsync(fileStream);

        return filePath;
    }

    public Task<Stream> GetFileAsync(string fileName)
    {
        var filePath = Path.Combine(_basePath, fileName);

        if (!File.Exists(filePath))
        {
            return Task.FromResult<Stream>(null);
        }

        return Task.FromResult<Stream>(new FileStream(filePath, FileMode.Open, FileAccess.Read));
    }

    public Task<bool> DeleteFileAsync(string fileName)
    {
        var filePath = Path.Combine(_basePath, fileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
