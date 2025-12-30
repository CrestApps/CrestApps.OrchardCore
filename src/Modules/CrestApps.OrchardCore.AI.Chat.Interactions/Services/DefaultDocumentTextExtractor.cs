using System.Text;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Services;

public sealed class DefaultDocumentTextExtractor : IDocumentTextExtractor
{
    private static readonly HashSet<string> _supportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".csv", ".md", ".json", ".xml",
            ".html", ".htm", ".log", ".yaml", ".yml",
        };

    public async Task<string> ExtractAsync(
        Stream stream,
        string fileName,
        string contentType)
    {
        var extension = Path.GetExtension(fileName);

        if (!_supportedExtensions.Contains(extension) &&
            contentType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) != true)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        return await reader.ReadToEndAsync();
    }
}
