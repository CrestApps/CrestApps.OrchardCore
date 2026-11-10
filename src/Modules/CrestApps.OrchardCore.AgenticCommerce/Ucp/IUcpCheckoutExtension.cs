using CrestApps.OrchardCore.AgenticCommerce.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// An extension of UCP checkout, such as AP2 mandates: it adds to every checkout answer, and may refuse to complete.
/// </summary>
public interface IUcpCheckoutExtension
{
    /// <summary>
    /// Adds the extension's members to a checkout answer, when the extension was agreed.
    /// </summary>
    /// <param name="context">The checkout being answered.</param>
    Task WritingAsync(UcpCheckoutContext context);

    /// <summary>
    /// Checks a complete request before the agent pays; a message refuses it.
    /// </summary>
    /// <param name="context">The checkout being completed, with the request's body.</param>
    Task<AgentCheckoutMessage> CompletingAsync(UcpCheckoutContext context);

    /// <summary>
    /// Runs once the checkout is completed, such as to keep evidence.
    /// </summary>
    /// <param name="context">The completed checkout, with the request's body.</param>
    Task CompletedAsync(UcpCheckoutContext context);
}
