namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// Gives what a tool is told about the platform.
/// </summary>
public interface ILtiPlatform
{
    /// <summary>
    /// Gets the platform's issuer, instance and endpoints.
    /// </summary>
    /// <returns>The platform.</returns>
    Task<LtiPlatformInfo> GetAsync();
}
