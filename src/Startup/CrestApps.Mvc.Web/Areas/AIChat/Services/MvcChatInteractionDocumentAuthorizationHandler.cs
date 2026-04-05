using CrestApps.AI;
using CrestApps.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace CrestApps.Mvc.Web.Areas.AIChat.Services;

public sealed class MvcChatInteractionDocumentAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, ChatInteraction>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        ChatInteraction resource)
    {
        if (resource != null &&
            requirement.Name == AIChatDocumentOperations.ManageDocuments.Name &&
            context.User.IsInRole("Administrator"))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
