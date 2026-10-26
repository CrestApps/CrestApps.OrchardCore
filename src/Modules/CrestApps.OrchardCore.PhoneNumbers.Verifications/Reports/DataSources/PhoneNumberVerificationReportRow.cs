using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Reports.DataSources;

/// <summary>
/// One row of the phone number verifications data set: a content item, its verification part, and the stored
/// verification result, read from the part once for every field of the row.
/// </summary>
public sealed class PhoneNumberVerificationReportRow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PhoneNumberVerificationReportRow"/> class.
    /// </summary>
    /// <param name="contentItem">The content item that owns the verification data.</param>
    /// <param name="part">The verification part of the content item.</param>
    /// <param name="result">The stored verification result, or <see langword="null"/> when there is none.</param>
    public PhoneNumberVerificationReportRow(
        ContentItem contentItem,
        PhoneNumberVerificationPart part,
        PhoneNumberVerificationResult result)
    {
        ContentItem = contentItem;
        Part = part;
        Result = result;
    }

    /// <summary>
    /// Gets the content item that owns the verification data.
    /// </summary>
    public ContentItem ContentItem { get; }

    /// <summary>
    /// Gets the verification part of the content item.
    /// </summary>
    public PhoneNumberVerificationPart Part { get; }

    /// <summary>
    /// Gets the stored verification result, or <see langword="null"/> when there is none.
    /// </summary>
    public PhoneNumberVerificationResult Result { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the record failed as many times in a row as the site allows, so it is no
    /// longer retried automatically.
    /// </summary>
    public bool IsExhausted { get; set; }
}
