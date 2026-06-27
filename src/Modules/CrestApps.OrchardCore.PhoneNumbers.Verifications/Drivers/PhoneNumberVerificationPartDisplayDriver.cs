using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.ViewModels;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Drivers;

/// <summary>
/// Displays the system-managed phone number verification data on a content item.
/// </summary>
public sealed class PhoneNumberVerificationPartDisplayDriver : ContentPartDisplayDriver<PhoneNumberVerificationPart>
{
    public override IDisplayResult Display(PhoneNumberVerificationPart part, BuildPartDisplayContext context)
    {
        return Initialize<PhoneNumberVerificationPartViewModel>("PhoneNumberVerificationPart", viewModel => BuildViewModel(viewModel, part))
            .Location("Detail", "Content:20")
            .Location("Summary", "Meta:20");
    }

    public override IDisplayResult Edit(PhoneNumberVerificationPart part, BuildPartEditorContext context)
    {
        return Initialize<PhoneNumberVerificationPartViewModel>("PhoneNumberVerificationPart_Edit", viewModel => BuildViewModel(viewModel, part))
            .Location("Content:20");
    }

    private static void BuildViewModel(PhoneNumberVerificationPartViewModel viewModel, PhoneNumberVerificationPart part)
    {
        viewModel.VerificationStatus = part.VerificationStatus;
        viewModel.VerificationProvider = part.VerificationProvider;
        viewModel.LastVerifiedUtc = part.LastVerifiedUtc;
        viewModel.NextVerificationDueUtc = part.NextVerificationDueUtc;
        viewModel.VerificationAttemptCount = part.VerificationAttemptCount;
        viewModel.FailedAttemptCount = part.FailedAttemptCount;
        viewModel.LastError = part.LastError;
        viewModel.LastAttemptUtc = part.LastAttemptUtc;
        viewModel.NormalizedPhoneNumber = part.NormalizedPhoneNumber ?? part.PhoneNumber;

        if (!part.TryGetPhoneNumberVerificationResult(out var result))
        {
            return;
        }

        viewModel.NormalizedPhoneNumber ??= result.NormalizedPhoneNumber ?? result.PhoneNumber;
        viewModel.Carrier = result.Carrier;
        viewModel.LineType = result.LineType;
    }
}
