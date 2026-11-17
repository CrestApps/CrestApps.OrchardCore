using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.Banking.AuditTrail;

/// <summary>
/// The Audit Trail events of bank accounts.
/// </summary>
internal sealed class BankingAuditTrailEvents : IConfigureOptions<AuditTrailOptions>
{
    public const string CategoryName = "Banking";
    public const string AccountCreated = "BankAccountCreated";
    public const string AccountUpdated = "BankAccountUpdated";
    public const string NumberChanged = "BankAccountNumberChanged";
    public const string StatusChanged = "BankAccountStatusChanged";
    public const string AccountDeleted = "BankAccountDeleted";
    public const string NumberRevealed = "BankAccountNumberRevealed";

    public void Configure(AuditTrailOptions options)
    {
        options.For<BankingAuditTrailEvents>(CategoryName, S => S["Banking"])
            .WithEvent(AccountCreated, S => S["Bank account added"], S => S["Someone added a bank account."], enableByDefault: true)
            .WithEvent(AccountUpdated, S => S["Bank account changed"], S => S["Someone changed the name, holder or bank of a bank account."], enableByDefault: true)
            .WithEvent(NumberChanged, S => S["Bank account number changed"], S => S["Someone changed the routing or account number of a bank account."], enableByDefault: true)
            .WithEvent(StatusChanged, S => S["Bank account status changed"], S => S["A bank account was verified, failed, closed or taken back to unverified."], enableByDefault: true)
            .WithEvent(AccountDeleted, S => S["Bank account removed"], S => S["Someone removed a bank account."], enableByDefault: true)
            .WithEvent(NumberRevealed, S => S["Bank account number shown"], S => S["Someone was shown a full bank account number, or it was written to a file or an export."], enableByDefault: true);
    }
}
