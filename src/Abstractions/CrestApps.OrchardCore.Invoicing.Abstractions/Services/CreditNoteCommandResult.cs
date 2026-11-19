using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// What a credit note command did.
/// </summary>
public sealed class CreditNoteCommandResult
{
    /// <summary>
    /// Gets whether the command did what was asked, or found it already done.
    /// </summary>
    public bool Succeeded
        => Errors.Count == 0;

    /// <summary>
    /// Gets or sets the credit note.
    /// </summary>
    public CreditNote CreditNote { get; set; }

    /// <summary>
    /// Gets or sets the invoice it credits.
    /// </summary>
    public Invoice Invoice { get; set; }

    /// <summary>
    /// Gets or sets whether the command was already done, so nothing changed.
    /// </summary>
    public bool AlreadyDone { get; set; }

    /// <summary>
    /// Gets why the command was refused.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Adds a reason the command was refused.
    /// </summary>
    public CreditNoteCommandResult Fail(string message)
    {
        Errors.Add(message);

        return this;
    }
}
