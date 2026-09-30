using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Marks a content type as an account: the company or household its contacts and opportunities belong to. The
/// account holds them through Orchard Core's <c>ListPart</c>, so the part itself carries no data.
/// </summary>
public sealed class AccountPart : ContentPart
{
}
