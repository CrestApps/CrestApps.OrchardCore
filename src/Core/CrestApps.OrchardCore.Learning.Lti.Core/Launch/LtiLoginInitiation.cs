using CrestApps.OrchardCore.Learning.Lti.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// The third-party initiated login (LTI 1.3 Core 5.1.1 and the Security Framework 5.1.1.1): the platform sends the
/// browser to the tool's login initiation URL with <c>iss</c>, <c>login_hint</c>, <c>target_link_uri</c>,
/// <c>lti_message_hint</c>, <c>client_id</c> and <c>lti_deployment_id</c>.
/// </summary>
public static class LtiLoginInitiation
{
    /// <summary>
    /// Builds the URL the browser is sent to.
    /// </summary>
    /// <param name="tool">The tool.</param>
    /// <param name="platform">The platform.</param>
    /// <param name="loginHint">The login hint: the id the tool knows the person by.</param>
    /// <param name="messageHint">The signed message hint.</param>
    /// <param name="targetLinkUri">The target link URI.</param>
    /// <returns>The tool's login initiation URL with the parameters in its query string.</returns>
    public static string BuildUrl(ExternalTool tool, LtiPlatformInfo platform, string loginHint, string messageHint, string targetLinkUri)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(platform);

        return QueryHelpers.AddQueryString(tool.LoginInitiationUrl, new Dictionary<string, string>
        {
            ["iss"] = platform.Issuer,
            ["login_hint"] = loginHint,
            ["target_link_uri"] = targetLinkUri,
            ["lti_message_hint"] = messageHint,
            ["client_id"] = tool.ClientId,
            ["lti_deployment_id"] = tool.DeploymentId,
        });
    }
}
