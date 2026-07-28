using CrestApps.OrchardCore.ContactCenter.Reports.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports;

/// <summary>
/// Maps each parameterized Contact Center report to the capability features that write the data it reads.
/// </summary>
/// <remarks>
/// The enterprise and workforce reports are served by a single provider class each, parameterized by a report
/// definition. Keeping the requirement beside the report kind — rather than on the registration call — means a new
/// report kind cannot be added without deciding what produces its data.
/// </remarks>
internal static class ContactCenterReportCapabilityRequirements
{
    private static readonly string[] _voice = [ContactCenterConstants.Feature.Voice];
    private static readonly string[] _recording = [ContactCenterConstants.Feature.Recording];

    /// <summary>
    /// Gets the features that must be enabled for an enterprise interaction report to have any data to measure.
    /// </summary>
    /// <param name="kind">The enterprise interaction report kind.</param>
    /// <returns>The required feature identifiers, or an empty collection when the reporting closure suffices.</returns>
    public static IReadOnlyCollection<string> For(EnterpriseInteractionReportKind kind)
    {
        return kind switch
        {
            // Interaction.ProviderName is only ever written by the voice call state and routing services.
            EnterpriseInteractionReportKind.ProviderPerformance => _voice,
            EnterpriseInteractionReportKind.ProviderUsageBilling => _voice,

            // Interaction.TransferHistory is only ever written by the voice transfer service.
            EnterpriseInteractionReportKind.TransferAnalysis => _voice,
            EnterpriseInteractionReportKind.QueueTransferPerformance => _voice,
            EnterpriseInteractionReportKind.AgentTransferPerformance => _voice,

            // Call legs are projected onto the call session by the voice call topology projector.
            EnterpriseInteractionReportKind.CallLegPerformance => _voice,

            // Interaction.RecordingReference is only ever written by the recording service.
            EnterpriseInteractionReportKind.RecordingCoverage => _recording,
            EnterpriseInteractionReportKind.AgentRecordingCoverage => _recording,

            _ => [],
        };
    }

    /// <summary>
    /// Gets the features that must be enabled for an agent workforce report to have any data to measure.
    /// </summary>
    /// <param name="kind">The agent workforce report kind.</param>
    /// <returns>The required feature identifiers, or an empty collection when the reporting closure suffices.</returns>
    /// <remarks>
    /// Every workforce report is built from agent presence events, which the availability capability writes and the
    /// reporting feature already depends on.
    /// </remarks>
    public static IReadOnlyCollection<string> For(AgentWorkforceReportKind kind) => [];
}
