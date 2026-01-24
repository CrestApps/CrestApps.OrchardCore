namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class ExtractorExtension : IEquatable<ExtractorExtension>, IEquatable<string>
{
    public string Extension { get; }

    public bool Embeddable { get; }

    public ExtractorExtension(string extension, bool embeddable = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        // Normalize once
        Extension = Normalize(extension);
        Embeddable = embeddable;
    }

    private static string Normalize(string extension)
    {
        var ext = extension.StartsWith('.') ? extension : "." + extension;

        return ext.ToLowerInvariant();
    }

    public bool Equals(ExtractorExtension other)
        => other is not null &&
           string.Equals(Extension, other.Extension, StringComparison.Ordinal);

    public bool Equals(string extension)
        => extension is not null &&
           string.Equals(Extension, Normalize(extension), StringComparison.Ordinal);

    public override bool Equals(object obj)
        => obj switch
        {
            ExtractorExtension ext => Equals(ext),
            string s => Equals(s),
            _ => false
        };

    public override int GetHashCode()
        => StringComparer.Ordinal.GetHashCode(Extension);

    public static bool operator ==(ExtractorExtension left, ExtractorExtension right)
        => Equals(left, right);

    public static bool operator !=(ExtractorExtension left, ExtractorExtension right)
        => !Equals(left, right);

    public static implicit operator string(ExtractorExtension ext)
        => ext.Extension;

    public static implicit operator ExtractorExtension(string extension)
        => new(extension);

    public override string ToString() => Extension;
}
