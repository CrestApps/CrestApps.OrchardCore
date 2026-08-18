namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Verifies a Stripe secret key and provisions (or removes) the webhook endpoint for a specific
/// environment (test or live), so an administrator can connect an account without creating the webhook by hand.
/// </summary>
public interface IStripeConnectService
{
    /// <summary>
    /// Determines whether a Stripe account has been verified and connected for the given environment.
    /// </summary>
    /// <param name="isLive"><see langword="true"/> to inspect the live environment; otherwise the test environment.</param>
    /// <returns><see langword="true"/> when an account is connected; otherwise, <see langword="false"/>.</returns>
    Task<bool> IsConnectedAsync(bool isLive);

    /// <summary>
    /// Verifies the Stripe secret key for the given environment and automatically provisions the webhook endpoint,
    /// persisting the resolved account identifier and the webhook signing secret.
    /// </summary>
    /// <param name="isLive"><see langword="true"/> to connect the live environment; otherwise the test environment.</param>
    /// <param name="publishableKey">The publishable key entered by the administrator, or <see langword="null"/> to keep the stored value.</param>
    /// <param name="secretKey">The secret key entered by the administrator, or <see langword="null"/> to use the stored value.</param>
    /// <param name="webhookUrl">The absolute URL of this site's Stripe webhook endpoint.</param>
    /// <returns>The result describing whether the account was connected successfully.</returns>
    Task<StripeConnectionResult> ConnectAsync(bool isLive, string publishableKey, string secretKey, string webhookUrl);

    /// <summary>
    /// Disconnects the configured Stripe account for the given environment, removing the provisioned webhook and
    /// clearing the stored credentials.
    /// </summary>
    /// <param name="isLive"><see langword="true"/> to disconnect the live environment; otherwise the test environment.</param>
    /// <returns>The result describing whether the account was disconnected successfully.</returns>
    Task<StripeConnectionResult> DisconnectAsync(bool isLive);
}
