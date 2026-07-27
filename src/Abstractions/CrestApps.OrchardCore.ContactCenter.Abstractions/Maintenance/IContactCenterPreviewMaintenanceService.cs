namespace CrestApps.OrchardCore.ContactCenter.Maintenance;

/// <summary>
/// Provides the operator-visible export, quiesce, reset, and verify procedure that makes a breaking Contact
/// Center data change recoverable on a preview tenant.
/// </summary>
public interface IContactCenterPreviewMaintenanceService
{
    /// <summary>
    /// Reads the live per-data-set document counts without modifying anything.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The live counts for every registered data set.</returns>
    Task<IReadOnlyList<ContactCenterPreviewDataSetCount>> GetDataSetCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the live maintenance state of the current tenant, including data set counts, the features that
    /// participate in quiesce, and whether reset is currently permitted.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The maintenance status.</returns>
    Task<ContactCenterPreviewMaintenanceStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes every Contact Center data set for the current tenant to the supplied stream as a self-describing
    /// JSON document, and returns the receipt that binds the export to the state it captured.
    /// </summary>
    /// <param name="destination">The stream the export is written to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The export report, including the receipt required by a subsequent reset.</returns>
    Task<ContactCenterPreviewExportReport> ExportAsync(Stream destination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes Contact Center work admission for every enabled Contact Center feature and waits for in-flight
    /// work to finish.
    /// </summary>
    /// <param name="drainTimeout">The maximum amount of time to wait for in-flight work to finish.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The quiesce report.</returns>
    Task<ContactCenterPreviewQuiesceReport> QuiesceAsync(TimeSpan drainTimeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reopens Contact Center work admission for every enabled Contact Center feature.
    /// </summary>
    /// <returns>The feature identifiers whose work admission was reopened.</returns>
    Task<IReadOnlyList<string>> ResumeAsync();

    /// <summary>
    /// Deletes the Contact Center data of the current tenant, refusing unless the deployment allows reset, the
    /// operator confirmed the tenant name, work admission is quiesced, and the supplied export receipt still
    /// matches the live state.
    /// </summary>
    /// <param name="request">The operator's reset request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The reset report.</returns>
    Task<ContactCenterPreviewResetReport> ResetAsync(ContactCenterPreviewResetRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies that every data set in the supplied scope is empty.
    /// </summary>
    /// <param name="scope">The scope to verify.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The verification report.</returns>
    Task<ContactCenterPreviewVerificationReport> VerifyAsync(ContactCenterPreviewResetScope scope, CancellationToken cancellationToken = default);
}
