using CrestApps.OrchardCore.ContactCenter;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Releases the agent's own leg once the call it was bridged to has ended.
/// </summary>
public sealed partial class TelnyxContactCenterVoiceProvider : IContactCenterVoiceAgentLegReleaseProvider
{
    /// <inheritdoc/>
    public async Task ReleaseAgentLegAsync(string agentLegId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(agentLegId))
        {
            return;
        }

        // The agent leg is bridged to the caller with the bridge command issued on the agent's leg, and Telnyx does not
        // reliably hang it up when the caller's leg goes. A leg that did go with the caller is refused here (422),
        // which is the expected answer.
        var result = await _apiClient.HangupAsync(agentLegId.Trim(), cancellationToken);

        if (!result.Succeeded && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Telnyx returned {StatusCode} hanging up an agent leg after its call ended (it may already be gone).",
                result.StatusCode);
        }
    }
}
