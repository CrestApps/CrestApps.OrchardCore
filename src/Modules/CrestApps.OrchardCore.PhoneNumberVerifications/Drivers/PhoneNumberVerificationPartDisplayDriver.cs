using System.Text.Json;
using CrestApps.OrchardCore.PhoneNumberVerifications.Models;
using CrestApps.OrchardCore.PhoneNumberVerifications.Services;
using CrestApps.OrchardCore.PhoneNumberVerifications.ViewModels;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.Drivers;

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

        if (string.IsNullOrEmpty(part.VerificationResultJson))
        {
            return;
        }

        try
        {
            var result = JsonSerializer.Deserialize<PhoneNumberVerificationResult>(part.VerificationResultJson, PhoneNumberVerificationSerialization.Options);

            if (result is not null)
            {
                viewModel.NormalizedPhoneNumber = result.NormalizedPhoneNumber ?? result.PhoneNumber;
                viewModel.Carrier = result.Carrier;
                viewModel.LineType = result.LineType;
            }
        }
        catch (JsonException)
        {
        }
    }
}
