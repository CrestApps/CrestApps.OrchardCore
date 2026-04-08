using CrestApps.Core.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Services;

public sealed class MvcAIChatSessionDocumentAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, AIChatSessionDocumentAuthorizationContext>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        AIChatSessionDocumentAuthorizationContext resource)
    {
        if (resource != null &&
            requirement.Name == AIChatDocumentOperations.ManageDocuments.Name &&
            context.User.Identity?.IsAuthenticated == true)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
