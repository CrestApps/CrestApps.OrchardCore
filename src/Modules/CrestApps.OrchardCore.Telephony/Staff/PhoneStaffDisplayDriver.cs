using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Telephony.Staff;

/// <summary>
/// The Phone card of the staff editor: the person's extension, with a link to change it or give them one.
/// </summary>
internal sealed class PhoneStaffDisplayDriver : DisplayDriver<StaffMember>
{
    private readonly ITelephonyExtensionManager _extensions;

    public PhoneStaffDisplayDriver(ITelephonyExtensionManager extensions)
    {
        _extensions = extensions;
    }

    public override async Task<IDisplayResult> EditAsync(StaffMember staff, BuildEditorContext context)
    {
        if (string.IsNullOrEmpty(staff.UserId))
        {
            return null;
        }

        var extension = await _extensions.FindByUserIdAsync(staff.UserId);

        return Initialize<PhoneStaffViewModel>("PhoneStaff_Edit", model =>
        {
            model.ExtensionId = extension?.ItemId;
            model.Number = extension?.Number;
            model.Name = extension?.Name;
        }).Location("Content:9");
    }
}
