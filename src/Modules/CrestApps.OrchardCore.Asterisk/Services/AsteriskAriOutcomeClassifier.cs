using System.Net;
using Polly.Timeout;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Classifies the outcome of a failed Asterisk ARI provisioning operation, distinguishing a definite server-side
/// rejection (safe to compensate and delete the durable record) from an ambiguous outcome (the operation may still
/// have committed on Asterisk, so the durable record must be retained for the age-gated reconciler).
/// </summary>
internal static class AsteriskAriOutcomeClassifier
{
    /// <summary>
    /// Determines whether a failed ARI provisioning operation had an ambiguous outcome, meaning the operation may
    /// still have taken effect on the Asterisk server even though the client observed a failure. An ambiguous outcome
    /// requires retaining the durable provisioning record so the age-gated reconciler can re-probe live ARI state and
    /// clean up a resource that materialized after the failure, instead of deleting the only record that tracks it.
    /// </summary>
    /// <param name="exception">The exception observed while performing (or awaiting) the provisioning operation.</param>
    /// <returns><see langword="true"/> when the outcome is ambiguous and the durable record must be retained; otherwise, <see langword="false"/>.</returns>
    public static bool IsProvisioningOutcomeAmbiguous(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is AsteriskAriException ariException)
        {
            // A definite client-rejection response (4xx) proves the operation did not take effect, so the record can
            // be compensated and deleted. A null status (the client never reached the server for a response) or a
            // server-error status (5xx, which may reflect a partially applied operation) is ambiguous: the resource
            // may exist, so the record must be retained for the reconciler.
            return ariException.StatusCode is null ||
                ariException.StatusCode >= HttpStatusCode.InternalServerError;
        }

        if (exception is OperationCanceledException or TimeoutRejectedException)
        {
            // A cancelled or timed-out ARI call abandons the await without observing the server outcome, so the
            // resource may still be materializing on Asterisk. The resilience pipeline surfaces an exhausted attempt
            // or total-request timeout as a Polly TimeoutRejectedException (which does not derive from
            // OperationCanceledException), so both must be treated as ambiguous. Retain the record so the age-gated
            // reconciler can re-probe live state instead of deleting the only record that tracks the possible resource.
            return true;
        }

        // Any other exception was raised after the awaited ARI operations returned normally (an ARI transport failure
        // surfaces as an AsteriskAriException, a resilience timeout as a TimeoutRejectedException, and a cancellation
        // as an OperationCanceledException), so the resources this flow created are known to exist and compensation can
        // destroy them reliably. The record can be removed once that compensation succeeds; retaining it here would
        // only delay cleanup of an already-tracked resource.
        return false;
    }
}
