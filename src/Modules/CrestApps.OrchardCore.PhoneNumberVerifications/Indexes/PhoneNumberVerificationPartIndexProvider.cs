using System.Text.Json;
using CrestApps.OrchardCore.PhoneNumberVerifications.Models;
using CrestApps.OrchardCore.PhoneNumberVerifications.Services;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.Indexes;

/// <summary>
/// Maps content items that carry a <see cref="PhoneNumberVerificationPart"/> into the
/// <see cref="PhoneNumberVerificationPartIndex"/> for reporting and querying.
/// </summary>
public sealed class PhoneNumberVerificationPartIndexProvider : IndexProvider<ContentItem>
{
    /// <inheritdoc/>
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<PhoneNumberVerificationPartIndex>()
            .Map(contentItem =>
            {
                if (!contentItem.Latest || !contentItem.TryGet<PhoneNumberVerificationPart>(out var part))
                {
                    return null;
                }

                var index = new PhoneNumberVerificationPartIndex
                {
                    ContentItemId = contentItem.ContentItemId,
                    IsVerified = part.VerificationStatus == PhoneNumberVerificationStatus.Verified,
                    VerificationStatus = part.VerificationStatus,
                    VerificationProvider = part.VerificationProvider,
                    LastVerifiedUtc = part.LastVerifiedUtc,
                    NextVerificationDueUtc = part.NextVerificationDueUtc,
                };

                if (!string.IsNullOrEmpty(part.VerificationResultJson))
                {
                    PhoneNumberVerificationResult result = null;

                    try
                    {
                        result = JsonSerializer.Deserialize<PhoneNumberVerificationResult>(part.VerificationResultJson, PhoneNumberVerificationSerialization.Options);
                    }
                    catch (JsonException)
                    {
                        result = null;
                    }

                    if (result is not null)
                    {
                        index.PhoneNumber = result.NormalizedPhoneNumber ?? result.PhoneNumber;
                        index.CountryCode = result.CountryCode;
                        index.Carrier = result.Carrier;
                        index.IsMobile = result.IsMobile;
                        index.IsLandline = result.IsLandline;
                        index.IsVoip = result.IsVoip;
                    }
                }

                return index;
            });
    }
}
