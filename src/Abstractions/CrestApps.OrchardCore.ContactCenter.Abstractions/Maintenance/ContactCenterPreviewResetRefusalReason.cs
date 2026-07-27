namespace CrestApps.OrchardCore.ContactCenter.Maintenance;

/// <summary>
/// Identifies why a Contact Center preview reset was refused.
/// </summary>
public enum ContactCenterPreviewResetRefusalReason
{
    /// <summary>
    /// The reset was not refused.
    /// </summary>
    None,

    /// <summary>
    /// Reset is not enabled for this tenant. It is disabled unless an operator opts in through configuration.
    /// </summary>
    ResetNotAllowed,

    /// <summary>
    /// The host is running in the Production environment and the deployment has not opted out of that guard.
    /// </summary>
    ProductionEnvironment,

    /// <summary>
    /// The confirmation token supplied by the operator does not match the tenant name.
    /// </summary>
    ConfirmationTokenMismatch,

    /// <summary>
    /// No export receipt was supplied, so there is no evidence that the data about to be destroyed was exported.
    /// </summary>
    ExportReceiptMissing,

    /// <summary>
    /// The supplied export receipt no longer matches the live tenant state, so the export is stale and does not
    /// cover everything the reset would destroy.
    /// </summary>
    ExportReceiptStale,

    /// <summary>
    /// Contact Center work admission is still open, so the tenant is still accepting work and a reset would race
    /// against live traffic.
    /// </summary>
    WorkNotQuiesced,
}
