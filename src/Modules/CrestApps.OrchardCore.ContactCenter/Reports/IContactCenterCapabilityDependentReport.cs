namespace CrestApps.OrchardCore.ContactCenter.Reports;

/// <summary>
/// Declares the Contact Center capability features that produce the data a report reads.
/// </summary>
/// <remarks>
/// A report whose subject matter is written by a feature the tenant has not enabled cannot honestly report zero,
/// because zero is a measurement and no measurement was taken. Declaring the producing features lets the report state
/// that the capability is absent instead of publishing a number that reads as an operational result.
/// </remarks>
public interface IContactCenterCapabilityDependentReport
{
    /// <summary>
    /// Gets the identifiers of the features that write the data this report reads, beyond those the reporting feature
    /// already depends on. An empty collection means the report is served entirely by the reporting feature's own
    /// dependency closure.
    /// </summary>
    IReadOnlyCollection<string> RequiredFeatureIds { get; }
}
