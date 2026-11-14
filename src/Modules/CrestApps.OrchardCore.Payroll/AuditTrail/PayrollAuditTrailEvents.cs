using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.Payroll.AuditTrail;

/// <summary>
/// The Audit Trail events of pay profiles.
/// </summary>
internal sealed class PayrollAuditTrailEvents : IConfigureOptions<AuditTrailOptions>
{
    public const string CategoryName = "Payroll";
    public const string TaxIdRevealed = "PayrollTaxIdRevealed";
    public const string TaxIdChanged = "PayrollTaxIdChanged";
    public const string ElectionAdded = "PayrollElectionAdded";
    public const string DistributionChanged = "PayrollDistributionChanged";
    public const string ProfileChanged = "PayrollPayProfileChanged";

    public void Configure(AuditTrailOptions options)
    {
        options.For<PayrollAuditTrailEvents>(CategoryName, S => S["Payroll"])
            .WithEvent(TaxIdRevealed, S => S["Tax id shown"], S => S["Someone was shown a person's full tax id, or it was written to an export."], enableByDefault: true)
            .WithEvent(TaxIdChanged, S => S["Tax id changed"], S => S["Someone entered, changed or removed a person's tax id."], enableByDefault: true)
            .WithEvent(ElectionAdded, S => S["Tax election changed"], S => S["Someone added or changed a person's tax election."], enableByDefault: true)
            .WithEvent(DistributionChanged, S => S["Net pay split changed"], S => S["Someone changed where a person's net pay goes."], enableByDefault: true)
            .WithEvent(ProfileChanged, S => S["Pay profile saved"], S => S["Someone saved a pay profile."], enableByDefault: false);
    }
}
