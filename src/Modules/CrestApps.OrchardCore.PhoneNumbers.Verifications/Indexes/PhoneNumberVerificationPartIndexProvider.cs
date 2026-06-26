using CrestApps.OrchardCore.PhoneNumbers.Core.Indexes;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Indexes;

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
                    PhoneNumber = part.PhoneNumber,
                    NormalizedPhoneNumber = part.NormalizedPhoneNumber,
                    IsVerified = part.VerificationStatus == PhoneNumberVerificationStatus.Verified,
                    VerificationStatus = part.VerificationStatus,
                    VerificationProvider = part.VerificationProvider,
                    LastVerifiedUtc = part.LastVerifiedUtc,
                    NextVerificationDueUtc = part.NextVerificationDueUtc,
                };

                if (part.TryGetPhoneNumberVerificationResult(out var result))
                {
                    index.PhoneNumber ??= result.PhoneNumber;
                    index.NormalizedPhoneNumber ??= result.NormalizedPhoneNumber ?? result.PhoneNumber;
                    index.CountryCode = result.CountryCode;
                    index.Carrier = result.Carrier;
                    index.IsMobile = result.IsMobile;
                    index.IsLandline = result.IsLandline;
                    index.IsVoip = result.IsVoip;
                    index.LineStatus = result.LineStatus;
                }

                return index;
            });
    }
}
