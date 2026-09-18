using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace CrestApps.Core.Omnichannel.Sms.Portal.Security;

/// <summary>
/// What an SMS portal service asks the host to authorize.
/// </summary>
/// <remarks>
/// One entry per question an SMS portal service actually asks. A host must supply a handler for
/// every operation here, because an unhandled requirement is a denial.
/// </remarks>
public static class SmsPortalOperations
{
    /// <summary>
    /// Read and act on any conversation, not only the ones the caller owns or serves.
    /// </summary>
    /// <remarks>
    /// The resource is the conversation. This is the supervisor question: a caller who does not hold
    /// it is narrowed to their own threads and their own queues rather than refused outright.
    /// </remarks>
    public static readonly OperationAuthorizationRequirement ViewAllConversations = new() { Name = nameof(ViewAllConversations) };

    /// <summary>
    /// Open the SMS portal at all.
    /// </summary>
    /// <remarks>
    /// Asked when a connection is established rather than per conversation, because a caller who should not be
    /// in the portal must not reach the point of subscribing to anybody's inbox.
    /// </remarks>
    public static readonly OperationAuthorizationRequirement UseSmsPortal = new() { Name = nameof(UseSmsPortal) };
}
