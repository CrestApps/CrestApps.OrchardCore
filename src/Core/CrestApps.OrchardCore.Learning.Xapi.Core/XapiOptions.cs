namespace CrestApps.OrchardCore.Learning.Xapi.Core;

/// <summary>
/// The limits of the Learning Record Store, which keep its store bounded. A request over a limit is refused with 413.
/// </summary>
public sealed class XapiOptions
{
    /// <summary>Gets or sets the largest request body, in bytes, attachments included.</summary>
    public long MaxRequestBytes { get; set; } = 20L * 1024 * 1024;

    /// <summary>Gets or sets the most statements one request may store.</summary>
    public int MaxStatementsPerRequest { get; set; } = 500;

    /// <summary>Gets or sets the largest statement, in bytes of JSON.</summary>
    public int MaxStatementBytes { get; set; } = 256 * 1024;

    /// <summary>Gets or sets the largest attachment content one request may carry, in bytes.</summary>
    public long MaxAttachmentBytes { get; set; } = 16L * 1024 * 1024;

    /// <summary>Gets or sets the largest state, activity profile or agent profile document, in bytes.</summary>
    public int MaxDocumentBytes { get; set; } = 1024 * 1024;

    /// <summary>Gets or sets the most statements one query returns, which a <c>limit</c> of 0 or none also means.</summary>
    public int MaxQueryLimit { get; set; } = 100;
}
