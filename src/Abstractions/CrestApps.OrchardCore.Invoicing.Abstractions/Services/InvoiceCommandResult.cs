using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// What became of an invoice command.
/// </summary>
public sealed class InvoiceCommandResult
{
    /// <summary>
    /// Gets a value indicating whether the command succeeded, now or before.
    /// </summary>
    public bool Succeeded
        => Errors.Count == 0;

    /// <summary>
    /// Gets or sets the invoice as saved, or <see langword="null"/> when none has the identifier.
    /// </summary>
    public Invoice Invoice { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the command had already completed, so nothing changed now.
    /// </summary>
    public bool AlreadyDone { get; set; }

    /// <summary>
    /// Gets the reasons the command was refused, ready to show to the person who asked.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Adds an error.
    /// </summary>
    /// <param name="message">The message to show.</param>
    /// <returns>The same result.</returns>
    public InvoiceCommandResult Fail(string message)
    {
        Errors.Add(message);

        return this;
    }
}
