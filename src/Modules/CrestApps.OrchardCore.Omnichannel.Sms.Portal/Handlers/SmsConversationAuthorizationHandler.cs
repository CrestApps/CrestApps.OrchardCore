using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Handlers;

/// <summary>
/// Narrows the <see cref="SmsPortalPermissions.UseSmsPortal"/> permission to the threads a caller actually owns
/// or serves. The portal permission alone says the caller may use the workspace; when the authorization resource
/// is an <see cref="SmsConversationAuthorizationResource"/>, this handler fails the requirement unless
/// <see cref="ISmsConversationAuthorizationService"/> allows the requested operation on that thread. Requests that
/// carry no conversation resource are left to the normal permission evaluation.
/// </summary>
internal sealed class SmsConversationAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly Lazy<ISmsConversationAuthorizationService> _conversationAuthorizationService;

    /// <param name="conversationAuthorizationService">
    /// The conversation authorization service, resolved lazily. The service asks the authorization system
    /// whether the caller is a supervisor, and the authorization system runs this handler, so eager
    /// construction closes that loop before either end exists.
    /// </param>
    public SmsConversationAuthorizationHandler(Lazy<ISmsConversationAuthorizationService> conversationAuthorizationService)
    {
        _conversationAuthorizationService = conversationAuthorizationService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (requirement.Permission.Name != SmsPortalPermissions.UseSmsPortal.Name)
        {
            return;
        }

        if (context.Resource is not SmsConversationAuthorizationResource resource || resource.Conversation is null)
        {
            return;
        }

        if (!await _conversationAuthorizationService.Value.AuthorizeAsync(context.User, resource.Conversation, resource.Operation))
        {
            context.Fail();
        }
    }
}
