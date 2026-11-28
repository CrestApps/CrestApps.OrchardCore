using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.Models;
using Microsoft.AspNetCore.Identity;
using OrchardCore;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Accounts.B2B.Services;

/// <summary>
/// The rules of an account's business buying settings, shared by the account editor's Business buying card and the AI
/// tools: the ranges of the terms, the order limits kept, who may be the punchout buyer, and when a punchout key is made.
/// Each caller words the problems itself.
/// </summary>
internal static class B2BAccountSettingsRules
{
    /// <summary>
    /// The most days an invoice can be due in.
    /// </summary>
    public const int MaxNetDays = 365;

    /// <summary>
    /// Returns what is wrong with the terms: net days out of 0 to 365, a deposit out of 0 to 100 percent, or a negative
    /// credit limit or monthly budget.
    /// </summary>
    /// <param name="netDays">The days an invoice is due in, or none.</param>
    /// <param name="depositPercent">The deposit, in percent.</param>
    /// <param name="creditLimit">The credit limit, or none.</param>
    /// <param name="monthlyBudget">The monthly budget, or none.</param>
    /// <returns>The problems, or an empty list.</returns>
    public static IReadOnlyList<B2BAccountSettingsProblem> CheckTerms(int? netDays, decimal depositPercent, decimal? creditLimit, decimal? monthlyBudget)
    {
        var problems = new List<B2BAccountSettingsProblem>();

        if (netDays is < 0 or > MaxNetDays)
        {
            problems.Add(B2BAccountSettingsProblem.NetDaysOutOfRange);
        }

        if (depositPercent is < 0m or > 100m)
        {
            problems.Add(B2BAccountSettingsProblem.DepositOutOfRange);
        }

        if (creditLimit is < 0m || monthlyBudget is < 0m)
        {
            problems.Add(B2BAccountSettingsProblem.NegativeAmount);
        }

        return problems;
    }

    /// <summary>
    /// Returns the order limits to keep: one for each user, the first given, and only limits above zero.
    /// </summary>
    /// <param name="rows">The limits by user id.</param>
    /// <returns>The limits.</returns>
    public static List<BuyerOrderLimit> CleanOrderLimits(IEnumerable<(string UserId, decimal? Limit)> rows)
    {
        var limits = new List<BuyerOrderLimit>();

        foreach (var (userId, limit) in rows ?? [])
        {
            if (string.IsNullOrEmpty(userId) || limit is not > 0m || limits.Any(item => item.UserId == userId))
            {
                continue;
            }

            limits.Add(new BuyerOrderLimit { UserId = userId, Limit = limit.Value });
        }

        return limits;
    }

    /// <summary>
    /// Checks the member the procurement system shops as: one of the account's members, and never a site administrator.
    /// </summary>
    /// <param name="userId">The user id, or none.</param>
    /// <param name="members">The account's members.</param>
    /// <param name="userManager">The user manager.</param>
    /// <returns>The problem, or <see langword="null"/> when the user may be the punchout buyer.</returns>
    public static async Task<B2BAccountSettingsProblem?> CheckPunchoutUserAsync(string userId, AccountMembersPart members, UserManager<IUser> userManager)
    {
        ArgumentNullException.ThrowIfNull(userManager);

        var user = string.IsNullOrEmpty(userId) ? null : await userManager.FindByIdAsync(userId);

        if (user is null || (members ?? new AccountMembersPart()).FindMember(userId) is null)
        {
            return B2BAccountSettingsProblem.PunchoutUserNotMember;
        }

        if (await userManager.IsInRoleAsync(user, OrchardCoreConstants.Roles.Administrator))
        {
            return B2BAccountSettingsProblem.PunchoutUserIsAdministrator;
        }

        return null;
    }

    /// <summary>
    /// Copies the business settings onto the part, tidied: blank texts and price list are none.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <param name="settings">The settings.</param>
    public static void ApplySettings(B2BAccountPart part, B2BAccountSettings settings)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(settings);

        part.Enabled = settings.Enabled;
        part.TaxId = string.IsNullOrWhiteSpace(settings.TaxId) ? null : settings.TaxId.Trim();
        part.BillingEmail = string.IsNullOrWhiteSpace(settings.BillingEmail) ? null : settings.BillingEmail.Trim();
        part.Terms = new AccountTerms
        {
            NetDays = settings.NetDays,
            DepositPercent = settings.DepositPercent,
            CreditLimit = settings.CreditLimit,
            RequirePurchaseOrder = settings.RequirePurchaseOrder,
        };
        part.MonthlyBudget = settings.MonthlyBudget;
        part.PriceListId = string.IsNullOrWhiteSpace(settings.PriceListId) ? null : settings.PriceListId;
        part.OrderLimits = settings.OrderLimits ?? [];
    }

    /// <summary>
    /// Turns punchout on or off and sets its buyer, keeping the key.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <param name="enabled">Whether punchout is on.</param>
    /// <param name="userId">The punchout buyer's user id, or none.</param>
    public static void ApplyPunchout(B2BAccountPart part, bool enabled, string userId)
    {
        ArgumentNullException.ThrowIfNull(part);

        part.Punchout ??= new AccountPunchout();
        part.Punchout.Enabled = enabled;
        part.Punchout.UserId = userId;
    }

    /// <summary>
    /// Makes a new punchout key when punchout is on and one was asked for or there is none yet. The part keeps its hash.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <param name="asked">Whether a new key was asked for.</param>
    /// <param name="utcNow">The current time.</param>
    /// <returns>The new key, to show once, or <see langword="null"/> when none was made.</returns>
    public static string IssuePunchoutKey(B2BAccountPart part, bool asked, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(part);

        if (part.Punchout is not { Enabled: true } punchout || !(asked || string.IsNullOrEmpty(punchout.KeyHash)))
        {
            return null;
        }

        var (key, hash) = PunchoutService.NewKey();

        punchout.KeyHash = hash;
        punchout.KeyCreatedUtc = utcNow;

        return key;
    }
}

/// <summary>
/// An account's business buying settings, without punchout.
/// </summary>
internal sealed class B2BAccountSettings
{
    public bool Enabled { get; set; }

    public string TaxId { get; set; }

    public string BillingEmail { get; set; }

    public int? NetDays { get; set; }

    public decimal DepositPercent { get; set; }

    public decimal? CreditLimit { get; set; }

    public bool RequirePurchaseOrder { get; set; }

    public decimal? MonthlyBudget { get; set; }

    public string PriceListId { get; set; }

    public List<BuyerOrderLimit> OrderLimits { get; set; } = [];

    /// <summary>
    /// Reads the settings stored on a part.
    /// </summary>
    /// <param name="part">The part, or <see langword="null"/>.</param>
    /// <returns>The settings.</returns>
    public static B2BAccountSettings From(B2BAccountPart part)
        => new()
        {
            Enabled = part?.Enabled == true,
            TaxId = part?.TaxId,
            BillingEmail = part?.BillingEmail,
            NetDays = part?.Terms?.NetDays,
            DepositPercent = part?.Terms?.DepositPercent ?? 0m,
            CreditLimit = part?.Terms?.CreditLimit,
            RequirePurchaseOrder = part?.Terms?.RequirePurchaseOrder == true,
            MonthlyBudget = part?.MonthlyBudget,
            PriceListId = part?.PriceListId,
            OrderLimits = [.. (part?.OrderLimits ?? []).Where(limit => limit is not null)],
        };
}

/// <summary>
/// What can be wrong with an account's business buying settings.
/// </summary>
internal enum B2BAccountSettingsProblem
{
    NetDaysOutOfRange,
    DepositOutOfRange,
    NegativeAmount,
    PunchoutUserNotMember,
    PunchoutUserIsAdministrator,
}
