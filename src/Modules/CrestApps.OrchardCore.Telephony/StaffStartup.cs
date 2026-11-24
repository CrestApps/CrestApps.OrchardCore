using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Telephony.Staff;
using CrestApps.OrchardCore.Staff.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Frees the extensions of staff who leave.
/// </summary>
[RequireFeatures(StaffConstants.Features.Staff)]
public sealed class StaffStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddStaffLifecycleObserver<TelephonyStaffLifecycleObserver>();
        services.AddDisplayDriver<StaffMember, PhoneStaffDisplayDriver>();
    }
}
