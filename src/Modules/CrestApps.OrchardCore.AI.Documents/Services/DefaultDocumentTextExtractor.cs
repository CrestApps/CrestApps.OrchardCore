using System.Text;

namespace CrestApps.OrchardCore.AI.Documents.Services;

public sealed class DefaultDocumentTextExtractor : IDocumentTextExtractor
{
    private static readonly Dictionary<string, HashSet<string>> _extensionContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".txt"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "text/plain",
            },

            [".csv"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "text/csv",
                "text/plain",
                "application/vnd.ms-excel",
            },

            [".md"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "text/markdown",
                "text/plain",
            },

            [".json"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "application/json",
                "text/json",
            },

            [".xml"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "application/xml",
                "text/xml",
            },

            [".html"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "text/html",
            },

            [".htm"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "text/html",
            },

            [".log"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "text/plain",
            },

            [".yaml"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "application/yaml",
                "application/x-yaml",
                "text/yaml",
                "text/plain",
            },

            [".yml"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "application/yaml",
                "application/x-yaml",
                "text/yaml",
                "text/plain",
            },
        };

    public async Task<string> ExtractAsync(
        Stream stream,
        string fileName,
        string extension,
        string contentType)
    {
        if (stream is null || stream.Length == 0 || string.IsNullOrEmpty(extension))
        {
            return string.Empty;
        }

        if (!_extensionContentTypes.TryGetValue(extension, out var allowedTypes))
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(contentType) && !allowedTypes.Contains(contentType))
        {
            return string.Empty;
        }

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        return await reader.ReadToEndAsync();
    }
}
