using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Adds Contact Center agent presence controls to the floating soft phone widget.
/// </summary>
public sealed class ContactCenterSoftPhoneWidgetExtensionProvider : ISoftPhoneWidgetExtensionProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IShapeFactory _shapeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSoftPhoneWidgetExtensionProvider"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="agentProfileManager">The agent profile manager.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    public ContactCenterSoftPhoneWidgetExtensionProvider(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IAgentProfileManager agentProfileManager,
        IShapeFactory shapeFactory)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _agentProfileManager = agentProfileManager;
        _shapeFactory = shapeFactory;
    }

    /// <inheritdoc/>
    public async Task BuildAsync(SoftPhoneWidgetExtensionContext context, CancellationToken cancellationToken = default)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true ||
            !await _authorizationService.AuthorizeAsync(user, ContactCenterPermissions.SignIntoQueues))
        {
            return;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var profile = await _agentProfileManager.FindByUserIdAsync(userId, cancellationToken);

        if (profile is null)
        {
            return;
        }

        var shape = await _shapeFactory.CreateAsync("ContactCenterSoftPhonePresence");
        shape.Properties["Profile"] = profile;

        context.Shapes.Add(shape);
    }
}
