using CrestApps.OrchardCore.Banking.Models;

namespace CrestApps.OrchardCore.Disbursements.Core.Nacha;

/// <summary>
/// What a bank's notice of change corrects on the payee's account: the routing number, the account number, the kind
/// of account, or several of them. A value left <see langword="null"/> is not changed.
/// </summary>
/// <param name="RoutingNumber">The corrected routing number.</param>
/// <param name="AccountNumber">The corrected account number.</param>
/// <param name="Kind">The corrected kind of account.</param>
public sealed record NachaChangeCorrection(string RoutingNumber, string AccountNumber, BankAccountKind? Kind)
{
    /// <summary>
    /// Reads the corrected data of a notice of change: C01 account number, C02 routing number, C03 both, C05 kind of
    /// account, C06 account number and kind, C07 all three.
    /// </summary>
    /// <param name="code">The change code.</param>
    /// <param name="data">The corrected data, as the addenda holds it (29 characters, trailing spaces trimmed).</param>
    /// <returns>The correction, or <see langword="null"/> for a code this site does not apply.</returns>
    public static NachaChangeCorrection Parse(string code, string data)
    {
        data = (data ?? string.Empty).PadRight(29);

        return code switch
        {
            "C01" => new NachaChangeCorrection(null, Value(data[..17]), null),
            "C02" => new NachaChangeCorrection(Value(data[..9]), null, null),
            "C03" => new NachaChangeCorrection(Value(data[..9]), Value(data[12..29]), null),
            "C05" => KindOf(data[..2]) is { } kind ? new NachaChangeCorrection(null, null, kind) : null,
            "C06" => new NachaChangeCorrection(null, Value(data[..17]), KindOf(data[20..22])),
            "C07" => new NachaChangeCorrection(Value(data[..9]), Value(data[9..26]), KindOf(data[26..28])),
            _ => null,
        };
    }

    private static string Value(string text)
        => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static BankAccountKind? KindOf(string transactionCode)
        => transactionCode switch
        {
            "22" or "23" or "27" or "28" => BankAccountKind.Checking,
            "32" or "33" or "37" or "38" => BankAccountKind.Savings,
            _ => null,
        };
}
