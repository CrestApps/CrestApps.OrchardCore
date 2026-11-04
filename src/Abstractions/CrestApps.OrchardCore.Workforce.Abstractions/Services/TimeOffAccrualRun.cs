namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// What an accrual run did.
/// </summary>
/// <param name="Workers">How many workers it looked at.</param>
/// <param name="Entries">How many ledger entries it wrote.</param>
public sealed record TimeOffAccrualRun(int Workers, int Entries);
