using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.Banking.Checks.AuditTrail;

/// <summary>
/// The Audit Trail events of checks.
/// </summary>
internal sealed class ChecksAuditTrailEvents : IConfigureOptions<AuditTrailOptions>
{
    public const string CategoryName = "Checks";
    public const string CheckUpdated = "CheckUpdated";

    public void Configure(AuditTrailOptions options)
    {
        options.For<ChecksAuditTrailEvents>(CategoryName, S => S["Checks"])
            .WithEvent(CheckUpdated, S => S["Check edited"], S => S["Someone changed the payee, address, amount, date or memo of a check that was waiting to print."], enableByDefault: true);
    }
}
