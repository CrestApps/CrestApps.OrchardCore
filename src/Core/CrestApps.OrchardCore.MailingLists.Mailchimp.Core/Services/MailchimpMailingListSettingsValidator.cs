using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Models;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// Checks the Mailchimp settings of a list kept in Mailchimp: the audience id and the merge tag of the welcome code.
/// An empty audience id means the list is not linked yet, and an empty merge tag means the default.
/// </summary>
internal sealed class MailchimpMailingListSettingsValidator : IMailingListSettingsValidator
{
    internal readonly IStringLocalizer S;

    public MailchimpMailingListSettingsValidator(IStringLocalizer<MailchimpMailingListSettingsValidator> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ValidationResult>> ValidateAsync(MailingList list, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(list);

        var errors = new List<ValidationResult>();

        if (!list.IsMailchimpList())
        {
            return Task.FromResult<IReadOnlyList<ValidationResult>>(errors);
        }

        var settings = list.GetMailchimpSettings();

        if (!string.IsNullOrEmpty(settings.AudienceId) && !MailchimpListSettingsExtensions.IsValidAudienceId(settings.AudienceId))
        {
            errors.Add(new ValidationResult(S["Enter the audience id as Mailchimp shows it (Audience > Settings > Audience name and defaults), letters and digits only."], [nameof(MailchimpListSettings.AudienceId)]));
        }

        if (!string.IsNullOrEmpty(settings.CouponMergeTag) && !MailchimpListSettingsExtensions.IsValidMergeTag(settings.CouponMergeTag))
        {
            errors.Add(new ValidationResult(S["A merge tag has up to 10 capital letters, digits and underscores, such as COUPON."], [nameof(MailchimpListSettings.CouponMergeTag)]));
        }

        return Task.FromResult<IReadOnlyList<ValidationResult>>(errors);
    }
}
