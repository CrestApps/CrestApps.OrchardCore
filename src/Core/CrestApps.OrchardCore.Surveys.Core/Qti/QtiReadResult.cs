namespace CrestApps.OrchardCore.Surveys.Core.Qti;

/// <summary>
/// A QTI file read: its items, or why the whole file was refused.
/// </summary>
public sealed class QtiReadResult
{
    /// <summary>Gets or sets the error, one of <see cref="QtiErrors"/>, or <see langword="null"/>.</summary>
    public string Error { get; set; }

    /// <summary>Gets or sets what the error names, such as the archive rule and entry.</summary>
    public string ErrorDetail { get; set; }

    /// <summary>Gets or sets the items, in the order of the manifest.</summary>
    public IReadOnlyList<QtiItem> Items { get; set; } = [];

    /// <summary>Gets whether the file was read.</summary>
    public bool Succeeded => Error is null;
}
