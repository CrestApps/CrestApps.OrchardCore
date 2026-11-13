namespace CrestApps.OrchardCore.Checks.Models;

/// <summary>
/// The fields a check layout places.
/// </summary>
public static class CheckFieldNames
{
    /// <summary>
    /// The check date.
    /// </summary>
    public const string Date = "Date";

    /// <summary>
    /// The payee's name on the "Pay to the order of" line.
    /// </summary>
    public const string Payee = "Payee";

    /// <summary>
    /// The payee's address, for a window envelope.
    /// </summary>
    public const string PayeeAddress = "PayeeAddress";

    /// <summary>
    /// The amount in figures.
    /// </summary>
    public const string Amount = "Amount";

    /// <summary>
    /// The amount in words.
    /// </summary>
    public const string AmountInWords = "AmountInWords";

    /// <summary>
    /// The memo.
    /// </summary>
    public const string Memo = "Memo";

    /// <summary>
    /// The check number, top right.
    /// </summary>
    public const string Number = "Number";

    /// <summary>
    /// The payer's name and address (blank stock only).
    /// </summary>
    public const string Payer = "Payer";

    /// <summary>
    /// The bank's name (blank stock only).
    /// </summary>
    public const string Bank = "Bank";

    /// <summary>
    /// The signature line and image.
    /// </summary>
    public const string Signature = "Signature";

    /// <summary>
    /// The second signature line and image.
    /// </summary>
    public const string SecondSignature = "SecondSignature";

    /// <summary>
    /// Gets every field, in the order the layout editor lists them.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Date,
        Payee,
        PayeeAddress,
        Amount,
        AmountInWords,
        Memo,
        Number,
        Payer,
        Bank,
        Signature,
        SecondSignature,
    ];
}
