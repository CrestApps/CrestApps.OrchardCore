namespace CrestApps.OrchardCore.Dialpad.Models;

/// <summary>
/// Defines the Dialpad environments the provider can target.
/// </summary>
public enum DialpadEnvironment
{
    /// <summary>
    /// The production Dialpad environment hosted at dialpad.com.
    /// </summary>
    Production = 0,

    /// <summary>
    /// The sandbox Dialpad environment hosted at sandbox.dialpad.com, used for development and testing.
    /// </summary>
    Sandbox = 1,
}
