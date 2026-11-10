using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.Checkout.Services;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// The rules of delegated 3-D Secure: when the bank asks the buyer to approve a payment, an agent that can run 3-D Secure
/// itself is given the store's acquirer details and completes the checkout again with the result, which the payment
/// provider imports into the same payment.
/// </summary>
internal static class DelegatedAuthentication
{
    /// <summary>
    /// The checkout property that says the agent can run 3-D Secure itself.
    /// </summary>
    internal const string AgentRunsThreeDSecureProperty = "agentRunsThreeDSecure";

    /// <summary>
    /// Returns whether the store turned delegated 3-D Secure on and gave the acquirer details agents need.
    /// </summary>
    /// <param name="settings">The store's agentic commerce settings.</param>
    internal static bool IsConfigured(AgenticCommerceSettings settings)
        => settings is not null &&
            settings.EnableDelegatedAuthentication &&
            !string.IsNullOrWhiteSpace(settings.AcquirerBin) &&
            settings.AcquirerCountry?.Trim().Length == 2 &&
            !string.IsNullOrWhiteSpace(settings.AcquirerMerchantId) &&
            !string.IsNullOrWhiteSpace(settings.AuthenticationMerchantName);

    /// <summary>
    /// Returns whether the agent said, when it made the checkout, that it can run 3-D Secure itself.
    /// </summary>
    /// <param name="checkout">The checkout.</param>
    internal static bool AgentRunsThreeDSecure(AgentCheckout checkout)
        => string.Equals(checkout?.GetProperty(AgentRunsThreeDSecureProperty), "true", StringComparison.Ordinal);

    /// <summary>
    /// Returns what the agent needs to run 3-D Secure with the card network's directory server.
    /// </summary>
    /// <param name="settings">The store's agentic commerce settings.</param>
    /// <param name="directoryServer">The directory server, one of <see cref="AgentDirectoryServers"/>.</param>
    internal static AgentAuthenticationMetadata BuildMetadata(AgenticCommerceSettings settings, string directoryServer)
        => new()
        {
            AcquirerBin = settings.AcquirerBin?.Trim(),
            AcquirerCountry = settings.AcquirerCountry?.Trim().ToUpperInvariant(),
            AcquirerMerchantId = settings.AcquirerMerchantId?.Trim(),
            MerchantName = settings.AuthenticationMerchantName?.Trim(),
            RequestorId = string.IsNullOrWhiteSpace(settings.ThreeDSecureRequestorId) ? null : settings.ThreeDSecureRequestorId.Trim(),
            DirectoryServer = directoryServer,
        };

    /// <summary>
    /// Returns what a complete request does with the checkout's pending authentication and the result it carries.
    /// </summary>
    /// <param name="pending">The authentication the checkout waits for, or <see langword="null"/>.</param>
    /// <param name="result">The result the request carries, or <see langword="null"/>.</param>
    internal static DelegatedAuthenticationStep Classify(AgentPendingAuthentication pending, ThreeDSecureResult result)
    {
        if (pending is null)
        {
            return DelegatedAuthenticationStep.None;
        }

        if (result is null)
        {
            return DelegatedAuthenticationStep.Missing;
        }

        if (result.GetInvalidMember() is not null)
        {
            return DelegatedAuthenticationStep.Invalid;
        }

        return result.AllowsPayment ? DelegatedAuthenticationStep.Pay : DelegatedAuthenticationStep.NotAuthenticated;
    }

    /// <summary>
    /// Returns whether a payment sends the agent's result to the provider: a valid result that lets the payment go ahead,
    /// asked for by the checkout, or offered up front by an agent that runs 3-D Secure to a store and provider that take
    /// it.
    /// </summary>
    /// <param name="result">The result the request carries, or <see langword="null"/>.</param>
    /// <param name="pending">The authentication the checkout waits for, or <see langword="null"/>.</param>
    /// <param name="usable">Whether the store, the agent and the provider can all use delegated 3-D Secure.</param>
    internal static bool SendsResult(ThreeDSecureResult result, AgentPendingAuthentication pending, bool usable)
        => result is not null &&
            result.AllowsPayment &&
            result.GetInvalidMember() is null &&
            (pending is not null || usable);
}

/// <summary>
/// What a complete request does with a checkout's pending 3-D Secure authentication.
/// </summary>
internal enum DelegatedAuthenticationStep
{
    /// <summary>
    /// The checkout doesn't wait for one; it is paid as usual.
    /// </summary>
    None,

    /// <summary>
    /// The request carries no result: it is refused with <c>requires_3ds</c>.
    /// </summary>
    Missing,

    /// <summary>
    /// The result is incomplete or malformed: it is refused.
    /// </summary>
    Invalid,

    /// <summary>
    /// The bank didn't authenticate the buyer: the payment is canceled and the agent told why.
    /// </summary>
    NotAuthenticated,

    /// <summary>
    /// The buyer was authenticated: the payment goes ahead with the result.
    /// </summary>
    Pay,
}
