using System.Security.Claims;
using CrestApps.Core.Security;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// Holds an ambient principal, standing in for the host's request context.
/// </summary>
internal sealed class FakeUserAccessor : IUserAccessor
{
    public ClaimsPrincipal User { get; set; }
}
