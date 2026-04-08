namespace CrestApps.Core.AI.Models;

public static class ChatDocumentsOptionsExtensions
{
    public static string GetAllowedFileExtensionsAcceptValue(this ChatDocumentsOptions options)
        => BuildAcceptValue(options?.AllowedFileExtensions);

    public static string GetAllowedFileExtensionsDisplayValue(this ChatDocumentsOptions options)
        => BuildDisplayValue(options?.AllowedFileExtensions);

    public static string GetEmbeddableFileExtensionsAcceptValue(this ChatDocumentsOptions options)
        => BuildAcceptValue(options?.EmbeddableFileExtensions);

    public static string GetEmbeddableFileExtensionsDisplayValue(this ChatDocumentsOptions options)
        => BuildDisplayValue(options?.EmbeddableFileExtensions);

    private static string BuildAcceptValue(IEnumerable<string> extensions)
        => string.Join(',', OrderExtensions(extensions));

    private static string BuildDisplayValue(IEnumerable<string> extensions)
        => string.Join(", ", OrderExtensions(extensions));

    private static string[] OrderExtensions(IEnumerable<string> extensions)
        => (extensions ?? [])
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
