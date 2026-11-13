using CrestApps.OrchardCore.Disbursements.Models;

namespace CrestApps.OrchardCore.Disbursements.Core.Nacha;

/// <summary>
/// Reads a bank's return and notice of change file in the Nacha format: each return addenda (type 99) gives the
/// original trace number and the return code, each notice of change addenda (type 98) the original trace number, the
/// change code and the corrected data.
/// </summary>
public static class NachaReturnFileReader
{
    private static readonly Dictionary<string, string> _returnReasons = new(StringComparer.Ordinal)
    {
        ["R01"] = "Insufficient funds",
        ["R02"] = "Account closed",
        ["R03"] = "No account, or the account can't be found",
        ["R04"] = "Invalid account number",
        ["R05"] = "Unauthorized debit to a consumer account",
        ["R06"] = "Returned at the originator's request",
        ["R07"] = "Authorization revoked",
        ["R08"] = "Payment stopped",
        ["R09"] = "Uncollected funds",
        ["R10"] = "Customer advises not authorized",
        ["R11"] = "Entry not in accordance with the authorization",
        ["R12"] = "Account sold to another bank",
        ["R13"] = "Invalid routing number",
        ["R14"] = "Account holder deceased",
        ["R15"] = "Beneficiary deceased",
        ["R16"] = "Account frozen",
        ["R17"] = "File record edit criteria",
        ["R20"] = "Non-transaction account",
        ["R23"] = "Credit refused by the receiver",
        ["R24"] = "Duplicate entry",
        ["R29"] = "Corporate customer advises not authorized",
    };

    /// <summary>
    /// Reads the file.
    /// </summary>
    /// <param name="text">The file.</param>
    /// <returns>The returns and the notices of change.</returns>
    public static (IReadOnlyList<DisbursementReturn> Returns, IReadOnlyList<DisbursementChangeNotice> Notices, IReadOnlyList<string> Problems) Read(string text)
    {
        var returns = new List<DisbursementReturn>();
        var notices = new List<DisbursementChangeNotice>();
        var problems = new List<string>();
        var lineNumber = 0;

        foreach (var raw in (text ?? string.Empty).Split('\n'))
        {
            lineNumber++;

            var line = raw.TrimEnd('\r');

            if (line.Length == 0 || line[0] != '7')
            {
                continue;
            }

            if (line.Length < 79)
            {
                problems.Add($"Line {lineNumber} is shorter than an addenda record.");

                continue;
            }

            var type = line.Substring(1, 2);
            var code = line.Substring(3, 3).Trim();
            var trace = line.Substring(6, 15).Trim();

            if (type == "99")
            {
                returns.Add(new DisbursementReturn(trace, code, _returnReasons.GetValueOrDefault(code, code)));
            }
            else if (type == "98")
            {
                notices.Add(new DisbursementChangeNotice(trace, code, line.Substring(35, 29).TrimEnd()));
            }
        }

        return (returns, notices, problems);
    }

    /// <summary>
    /// Returns the description of a return code, or the code.
    /// </summary>
    /// <param name="code">The code, such as R01.</param>
    /// <returns>The description.</returns>
    public static string DescribeReturn(string code)
        => _returnReasons.GetValueOrDefault(code ?? string.Empty, code);
}
