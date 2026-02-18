namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class ChatDocumentsOptions
{
    private readonly HashSet<string> _allowedFileExtensions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _embeddableFileExtensions = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> AllowedFileExtensions => _allowedFileExtensions;

    public IReadOnlySet<string> EmbeddableFileExtensions => _embeddableFileExtensions;

    internal void Add(string extension, bool embeddable = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        var normalized = extension.StartsWith('.') ? extension : "." + extension;

        _allowedFileExtensions.Add(normalized);

        if (embeddable)
        {
            _embeddableFileExtensions.Add(normalized);
        }
        else
        {
            _embeddableFileExtensions.Remove(normalized);
        }
    }

    internal void Add(ExtractorExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        Add(extension.Extension, extension.Embeddable);
    }
}
