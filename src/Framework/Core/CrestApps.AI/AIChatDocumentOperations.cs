using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace CrestApps.AI;

public static class AIChatDocumentOperations
{
    public static OperationAuthorizationRequirement ManageDocuments { get; } = new()
    {
        Name = nameof(ManageDocuments),
    };
}
