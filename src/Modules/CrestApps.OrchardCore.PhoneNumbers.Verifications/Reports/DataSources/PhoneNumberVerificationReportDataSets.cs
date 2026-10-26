namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Reports.DataSources;

/// <summary>
/// The stable names of the phone number verifications report data source, its data set, and the fields other data sets
/// join on. Report designs store them, so they must never change.
/// </summary>
public static class PhoneNumberVerificationReportDataSets
{
    /// <summary>
    /// The technical name of the phone number verifications data source.
    /// </summary>
    public const string Source = "PhoneNumberVerifications";

    /// <summary>
    /// The verifications data set: one row per content item that carries phone number verification data.
    /// </summary>
    public const string Verifications = "Verifications";

    /// <summary>
    /// The name of the field that identifies the content item that owns the verification data.
    /// </summary>
    public const string ContentItemIdField = "ContentItemId";

    /// <summary>
    /// The name of the field that holds the normalized (E.164) phone number.
    /// </summary>
    public const string NormalizedPhoneNumberField = "NormalizedPhoneNumber";
}
