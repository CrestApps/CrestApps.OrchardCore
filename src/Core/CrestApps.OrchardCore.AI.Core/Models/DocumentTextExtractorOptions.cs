namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class DocumentTextExtractorOptions
{
    private readonly HashSet<string> _allowedFileExtensions = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> AllowedFileExtensions => _allowedFileExtensions;

    internal void Add(string extension)
        => _allowedFileExtensions.Add(extension);
}
