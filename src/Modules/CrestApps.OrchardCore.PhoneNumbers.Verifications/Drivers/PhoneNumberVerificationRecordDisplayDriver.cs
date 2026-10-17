using CrestApps.OrchardCore.PhoneNumbers.Core.Permissions;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Drivers;

/// <summary>
/// Displays a phone number verification record as a row of the phone number verification records queue, in
/// the List and Grid layouts of the admin list.
/// </summary>
public sealed class PhoneNumberVerificationRecordDisplayDriver : DisplayDriver<PhoneNumberVerificationRecord>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private bool? _canVerifyPhoneNumbers;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhoneNumberVerificationRecordDisplayDriver"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service used to decide whether the user can retry verifications.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public PhoneNumberVerificationRecordDisplayDriver(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task<IDisplayResult> DisplayAsync(PhoneNumberVerificationRecord record, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{record.NormalizedPhoneNumber} {record.PhoneNumber}".Trim().ToLowerInvariant();
        }

        return CombineAsync(
            View("PhoneNumberVerificationRecord_Checkbox_SummaryAdmin", record)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Checkbox:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Select:1")
                .RenderWhen(CanVerifyPhoneNumbersAsync),
            View("PhoneNumberVerificationRecord_Fields_SummaryAdmin", record)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("PhoneNumberVerificationRecord_Description_SummaryAdmin", record)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(record.DisplayText) || !string.IsNullOrEmpty(record.LastError))),
            View("PhoneNumberVerificationRecord_Status_SummaryAdmin", record)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("PhoneNumberVerificationRecord_Provider_SummaryAdmin", record)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Provider:5"),
            View("PhoneNumberVerificationRecord_Attempts_SummaryAdmin", record)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Attempts:5"),

            // A record whose last request failed offers the retry as a button; any other record can be verified
            // again from the actions menu.
            View("PhoneNumberVerificationRecord_Buttons_SummaryAdmin", record)
                .Location("Actions:5")
                .RenderWhen(async () => record.FailedAttemptCount > 0 && await CanVerifyPhoneNumbersAsync()),
            View("PhoneNumberVerificationRecord_ActionsMenu_SummaryAdmin", record)
                .Location("ActionsMenu:5")
                .RenderWhen(async () => record.FailedAttemptCount == 0 && await CanVerifyPhoneNumbersAsync())
        );
    }

    private async Task<bool> CanVerifyPhoneNumbersAsync()
    {
        _canVerifyPhoneNumbers ??= await _authorizationService.AuthorizeAsync(
            _httpContextAccessor.HttpContext?.User,
            PhoneNumberVerificationsPermissions.VerifyPhoneNumbers);

        return _canVerifyPhoneNumbers.Value;
    }
}
