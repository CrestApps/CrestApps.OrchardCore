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
    public const string RunSubmitted = "PayrollPayRunSubmitted";
    public const string RunDecided = "PayrollPayRunDecided";
    public const string RunProcessed = "PayrollPayRunProcessed";
    public const string RunCanceled = "PayrollPayRunCanceled";
    public const string StatementVoided = "PayrollPayStatementVoided";

    public void Configure(AuditTrailOptions options)
    {
        options.For<PayrollAuditTrailEvents>(CategoryName, S => S["Payroll"])
            .WithEvent(TaxIdRevealed, S => S["Tax id shown"], S => S["Someone was shown a person's full tax id, or it was written to an export."], enableByDefault: true)
            .WithEvent(TaxIdChanged, S => S["Tax id changed"], S => S["Someone entered, changed or removed a person's tax id."], enableByDefault: true)
            .WithEvent(ElectionAdded, S => S["Tax election changed"], S => S["Someone added or changed a person's tax election."], enableByDefault: true)
            .WithEvent(DistributionChanged, S => S["Net pay split changed"], S => S["Someone changed where a person's net pay goes."], enableByDefault: true)
            .WithEvent(ProfileChanged, S => S["Pay profile saved"], S => S["Someone saved a pay profile."], enableByDefault: false)
            .WithEvent(RunSubmitted, S => S["Pay run submitted"], S => S["Someone submitted a pay run for approval."], enableByDefault: true)
            .WithEvent(RunDecided, S => S["Pay run approved or rejected"], S => S["Someone approved or rejected a pay run."], enableByDefault: true)
            .WithEvent(RunProcessed, S => S["Pay run processed"], S => S["Someone processed a pay run and paid it, in full or in part."], enableByDefault: true)
            .WithEvent(RunCanceled, S => S["Pay run cancelled"], S => S["Someone cancelled a pay run."], enableByDefault: true)
            .WithEvent(StatementVoided, S => S["Pay statement voided"], S => S["Someone voided a pay statement."], enableByDefault: true);
    }
}
