namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The operator's declaration that the base-voice audio path has been verified for this deployment.
/// </summary>
/// <remarks>
/// Bound from the <c>CrestApps_ContactCenter:BaseVoiceVerification</c> configuration section. Whether the
/// end-to-end WebRTC media path works — trusted certificates, TURN relay, direct ICE, restart drain, and a
/// measured capacity floor — is a property of a <em>deployment</em> and its infrastructure, not of the
/// capability code, so it cannot be proven in the application build. It is proven once against the reference
/// topology and then declared here, exactly as the deployment topology is declared in
/// <see cref="ContactCenterTopologyOptions"/>. Until it is declared, a production host withholds readiness so
/// an unverified base-voice deployment never serves traffic.
/// </remarks>
public sealed class BaseVoiceVerificationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the operator has performed and acknowledged the base-voice audio
    /// verification step for this deployment.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="false"/>. In a production host environment an unacknowledged deployment fails
    /// readiness closed rather than merely warning, because serving voice traffic from a deployment whose media
    /// path was never proven is the failure this gate exists to prevent. Outside a production host environment
    /// the gate only warns, so development and test hosts are not blocked.
    /// </remarks>
    public bool AudioVerificationAcknowledged { get; set; }

    /// <summary>
    /// Gets or sets a reference to the retained evidence of the base-voice audio verification, such as a link or
    /// identifier for the captured proof run.
    /// </summary>
    /// <remarks>
    /// Required whenever <see cref="AudioVerificationAcknowledged"/> is <see langword="true"/>: an acknowledgment
    /// that cites no retained evidence is rejected at startup, so the acknowledgment can never be a bare boolean
    /// flip. It is surfaced in the readiness verdict so an operator can trace the acknowledgment to its evidence.
    /// </remarks>
    public string AudioVerificationEvidenceReference { get; set; }
}
