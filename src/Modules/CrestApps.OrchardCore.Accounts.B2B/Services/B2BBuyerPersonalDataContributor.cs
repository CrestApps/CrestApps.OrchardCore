using System.Globalization;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Accounts.B2B.Services;

/// <summary>
/// Exports and anonymizes what business buying keeps about a person who buys for an account: their spending limit and
/// whether punchout carts sign in as them. Erasing removes their spending limit and turns off a punchout that signs in as
/// them. Their membership itself belongs to the account's members, which the Accounts module manages.
/// </summary>
public sealed class B2BBuyerPersonalDataContributor : IPersonalDataContributor
{
    /// <summary>The record kind of a business buyer.</summary>
    public const string BuyerKind = "B2BBuyer";

    private readonly IB2BAccountStore _accounts;

    /// <summary>
    /// Initializes a new instance of the <see cref="B2BBuyerPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="accounts">The business account store.</param>
    public B2BBuyerPersonalDataContributor(IB2BAccountStore accounts)
    {
        _accounts = accounts;
    }

    /// <inheritdoc/>
    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var userId in context.Subject.UserIds)
        {
            if (await _accounts.FindByBuyerAsync(userId, cancellationToken) is not { } account)
            {
                continue;
            }

            var buyer = account.FindBuyer(userId);
            var record = context.Add(BuyerKind, account.AccountId, account.Name);

            record.Fields["AccountId"] = account.AccountId;
            record.Fields["AccountName"] = account.Name;
            record.Fields[nameof(AccountBuyer.Role)] = buyer?.Role.ToString();
            record.Fields[nameof(AccountBuyer.OrderLimit)] = buyer?.OrderLimit?.ToString(CultureInfo.InvariantCulture);
            record.Fields["PunchoutUser"] = IsPunchoutUser(account, userId).ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var userId in context.Subject.UserIds)
        {
            if (await _accounts.FindByBuyerAsync(userId, cancellationToken) is not { } account)
            {
                continue;
            }

            var changed = (account.Part.OrderLimits ?? []).RemoveAll(limit => string.Equals(limit?.UserId, userId, StringComparison.Ordinal)) > 0;

            if (IsPunchoutUser(account, userId))
            {
                account.Part.Punchout.Enabled = false;
                account.Part.Punchout.UserId = null;
                account.Part.Punchout.KeyHash = null;
                account.Part.Punchout.KeyCreatedUtc = null;
                changed = true;
            }

            if (!changed)
            {
                continue;
            }

            await _accounts.SaveAsync(account);
            context.Result.Count(BuyerKind);
        }
    }

    private static bool IsPunchoutUser(B2BAccount account, string userId)
        => account.Part.Punchout is { } punchout && string.Equals(punchout.UserId, userId, StringComparison.Ordinal);
}
