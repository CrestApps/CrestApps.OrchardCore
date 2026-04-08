using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace CrestApps.Core.AI;

public static class AIChatDocumentOperations
{
    public static OperationAuthorizationRequirement ManageDocuments { get; } = new()
    {
        Name = nameof(ManageDocuments),
    };
}
