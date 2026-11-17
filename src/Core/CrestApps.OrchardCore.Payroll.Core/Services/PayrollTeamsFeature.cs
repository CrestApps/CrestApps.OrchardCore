namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Registered only while the Teams feature is on. Staff registers the team services whatever features are on, but the
/// teams' tables exist only once Teams is enabled, so Payroll reads teams (departments, work sites, ledger codes) only
/// when this marker is there.
/// </summary>
public sealed class PayrollTeamsFeature
{
}
