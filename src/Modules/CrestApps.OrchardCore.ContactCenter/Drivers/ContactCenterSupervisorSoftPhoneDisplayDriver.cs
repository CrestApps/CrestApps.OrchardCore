using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

/// <summary>
/// Adds a supervisor's engagement banner to their soft phone: the call they are listening to, a mode switcher and Stop.
/// Its script tells the phone to answer the one leg it is rung on for each engagement the supervisor starts.
/// </summary>
internal sealed class ContactCenterSupervisorSoftPhoneDisplayDriver : DisplayDriver<SoftPhoneWidget>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IResourceManager _resourceManager;

    public ContactCenterSupervisorSoftPhoneDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IResourceManager resourceManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _resourceManager = resourceManager;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> DisplayAsync(SoftPhoneWidget widget, BuildDisplayContext context)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext?.User?.Identity?.IsAuthenticated != true ||
            !await _authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.MonitorContactCenter))
        {
            return null;
        }

        _resourceManager.RegisterResource("script", "contact-center-realtime").AtFoot();
        _resourceManager.RegisterResource("script", "contact-center-supervisor-phone").AtFoot();

        return View("ContactCenterSupervisorMonitor_Banner", new SupervisorSoftPhoneViewModel
        {
            HubUrl = SignalRHubRoutes.GetTenantAwareHubUrl<ContactCenterHub>(httpContext),
        }).Location("Detail", "HeaderActions:6");
    }
}
