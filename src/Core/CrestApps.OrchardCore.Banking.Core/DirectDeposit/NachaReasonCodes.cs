using Microsoft.Extensions.Localization;

using CrestApps.OrchardCore.Banking.Disbursements;
using CrestApps.OrchardCore.Banking.Disbursements.Core;

namespace CrestApps.OrchardCore.Banking.DirectDeposit;

/// <summary>
/// The text of bank return codes, localized where it is shown: disbursements store the code only.
/// </summary>
public static class NachaReasonCodes
{
    /// <summary>
    /// Describes a return code, or returns <see langword="null"/> for a code this site doesn't know.
    /// </summary>
    /// <param name="code">The code, such as R01.</param>
    /// <param name="S">The localizer of the page.</param>
    /// <returns>The description, or <see langword="null"/>.</returns>
    public static LocalizedString Describe(string code, IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(S);

        return code switch
        {
            "R01" => S["Insufficient funds"],
            "R02" => S["Account closed"],
            "R03" => S["No account, or the account can't be found"],
            "R04" => S["Invalid account number"],
            "R05" => S["Unauthorized debit to a consumer account"],
            "R06" => S["Returned at the originator's request"],
            "R07" => S["Authorization revoked"],
            "R08" => S["Payment stopped"],
            "R09" => S["Uncollected funds"],
            "R10" => S["The customer says the payment was not authorized"],
            "R11" => S["The entry does not match what was authorized"],
            "R12" => S["The account was sold to another bank"],
            "R13" => S["Invalid routing number"],
            "R14" => S["The account holder died"],
            "R15" => S["The beneficiary died"],
            "R16" => S["Account frozen"],
            "R17" => S["The entry could not be processed"],
            "R20" => S["Not a transaction account"],
            "R23" => S["Credit refused by the receiver"],
            "R24" => S["Duplicate entry"],
            "R29" => S["Corporate customer advises not authorized"],
            _ => null,
        };
    }
}
