using System.Text.RegularExpressions;

namespace CrestApps.Core.Mvc.Web.Services;

public sealed class FileSystemFileStore
{
    private static readonly Regex _safePathSegmentExpression = new("^[a-zA-Z0-9._-]+$", RegexOptions.Compiled);
    private readonly string _basePath;

    public FileSystemFileStore(string basePath)
    {
        _basePath = Path.GetFullPath(basePath);
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveFileAsync(string fileName, Stream content)
    {
        var filePath = GetSafePath(fileName);
        var directory = Path.GetDirectoryName(filePath);
        Directory.CreateDirectory(directory);

        using var fileStream = new FileStream(filePath, FileMode.Create);
        await content.CopyToAsync(fileStream);

        return filePath;
    }

    public Task<Stream> GetFileAsync(string fileName)
    {
        var filePath = GetSafePath(fileName);

        if (!File.Exists(filePath))
        {
            return Task.FromResult<Stream>(null);
        }

        return Task.FromResult<Stream>(new FileStream(filePath, FileMode.Open, FileAccess.Read));
    }

    public Task<bool> DeleteFileAsync(string fileName)
    {
        var filePath = GetSafePath(fileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);

            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    private string GetSafePath(string fileName)
    {
        var relativePath = NormalizeRelativePath(fileName);
        var fullPath = Path.GetFullPath(Path.Combine(_basePath, relativePath));

        if (!fullPath.StartsWith(_basePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fullPath, _basePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The file name contains an invalid path.");
        }

        return fullPath;
    }

    private static string NormalizeRelativePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.IsPathRooted(fileName))
        {
            throw new ArgumentException("The file name contains an invalid path.");
        }

        var segments = fileName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            throw new ArgumentException("The file name contains an invalid path.");
        }

        foreach (var segment in segments)
        {
            if (segment is "." or ".." || !_safePathSegmentExpression.IsMatch(segment))
            {
                throw new ArgumentException("The file name contains an invalid path.");
            }
        }

        return Path.Combine(segments);
    }
}
