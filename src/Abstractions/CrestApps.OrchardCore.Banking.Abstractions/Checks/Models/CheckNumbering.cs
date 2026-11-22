namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// How a check gets its number.
/// </summary>
public enum CheckNumbering
{
    /// <summary>
    /// The next number of the bank account's number series, given when the check is issued. The check is
    /// <see cref="CheckStatus.Issued"/> until it is printed.
    /// </summary>
    Next,

    /// <summary>
    /// No number until it is printed on pre-printed stock: the check is <see cref="CheckStatus.Queued"/>, and the print
    /// run gives it the number of the sheet it prints on.
    /// </summary>
    AtPrint,

    /// <summary>
    /// Written by hand with the number the person typed, which no other check of the account may have. The check is
    /// recorded as <see cref="CheckStatus.Printed"/> and is never printed by the site.
    /// </summary>
    Handwritten,
}
