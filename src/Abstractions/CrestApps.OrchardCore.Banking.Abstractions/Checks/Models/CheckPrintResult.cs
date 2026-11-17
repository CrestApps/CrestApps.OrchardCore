namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// The PDF of printed checks, or why they could not print.
/// </summary>
public sealed class CheckPrintResult
{
    /// <summary>
    /// Gets or sets the PDF file.
    /// </summary>
    public byte[] Pdf { get; set; }

    /// <summary>
    /// Gets or sets why the checks could not print.
    /// </summary>
    public IReadOnlyList<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets whether the PDF was made.
    /// </summary>
    public bool Succeeded
        => Pdf is { Length: > 0 } && Errors.Count == 0;
}
