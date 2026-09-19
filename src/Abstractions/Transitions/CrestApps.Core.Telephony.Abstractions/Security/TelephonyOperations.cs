using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace CrestApps.Core.Telephony.Security;

/// <summary>
/// What a telephony service asks the host to authorize.
/// </summary>
/// <remarks>
/// <para>
/// An operation says what the caller is trying to do; the host decides who may do it. That keeps the
/// suite from naming the host's permissions, which are the host's to define, rename and group.
/// </para>
/// <para>
/// One entry per question a telephony service actually asks. A host must supply a handler for every
/// operation here, because an unhandled requirement is a denial: a missing registration takes the
/// feature away rather than opening it up.
/// </para>
/// </remarks>
public static class TelephonyOperations
{
    /// <summary>
    /// Place and control calls from the soft phone.
    /// </summary>
    /// <remarks>
    /// Asked without a resource, and asked again on every call the soft phone makes rather than only
    /// when its connection opens, because a grant can be withdrawn while a connection is still up.
    /// </remarks>
    public static readonly OperationAuthorizationRequirement UseSoftPhone = new() { Name = nameof(UseSoftPhone) };
}
