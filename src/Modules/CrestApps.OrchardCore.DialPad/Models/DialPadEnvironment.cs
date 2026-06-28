namespace CrestApps.OrchardCore.DialPad.Models;

/// <summary>
/// Defines the DialPad environments the provider can target.
/// </summary>
public enum DialPadEnvironment
{
    /// <summary>
    /// The production DialPad environment hosted at dialpad.com.
    /// </summary>
    Production = 0,

    /// <summary>
    /// The sandbox DialPad environment hosted at sandbox.dialpad.com, used for development and testing.
    /// </summary>
    Sandbox = 1,
}
