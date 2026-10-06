namespace CrestApps.OrchardCore.Telnyx.Models;

/// <summary>
/// How hard the Telnyx noise suppression engine works on a voice. Stronger suppression removes more noise but also
/// more of the quiet tail of each word.
/// </summary>
/// <remarks>
/// <see cref="Balanced"/> is the default and has the value zero, so settings saved before the strength existed read
/// as balanced.
/// </remarks>
public enum TelnyxNoiseSuppressionStrength
{
    /// <summary>
    /// Removes steady line hiss and most background sound while keeping the soft endings of words. The default.
    /// </summary>
    Balanced = 0,

    /// <summary>
    /// Takes the edge off background sound and leaves the voice closest to how it was spoken.
    /// </summary>
    Light = 1,

    /// <summary>
    /// The engine at full strength. Removes the most noise, but can clip the quiet end of a word and make a voice sound
    /// louder and less natural.
    /// </summary>
    Strong = 2,
}
