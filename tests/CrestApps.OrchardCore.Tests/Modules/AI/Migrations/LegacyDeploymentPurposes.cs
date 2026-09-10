using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Migrations;

/// <summary>
/// Reaches the migrations' private copy of the legacy deployment purpose flags.
/// </summary>
/// <remarks>
/// The framework enum was removed when the deployment purpose became model capabilities. The migrations
/// keep an internal copy so they can still read documents written while it existed, and these tests name
/// its values as the raw bit flags a stored document carries.
/// </remarks>
internal static class LegacyDeploymentPurposes
{
    public const int None = 0;
    public const int Chat = 1 << 0;
    public const int Utility = 1 << 1;
    public const int Embedding = 1 << 2;
    public const int Image = 1 << 3;
    public const int SpeechToText = 1 << 4;
    public const int TextToSpeech = 1 << 5;
    public const int Vision = 1 << 6;

    public static Type PurposeType { get; } = typeof(Startup).Assembly.GetType(
        "CrestApps.OrchardCore.AI.Migrations.LegacyAIDeploymentPurpose",
        throwOnError: true)!;

    /// <summary>
    /// Boxes the given flags as the internal enum, so they can be passed to a reflected method.
    /// </summary>
    public static object Box(int flags)
        => Enum.ToObject(PurposeType, flags);

    /// <summary>
    /// Unboxes the internal enum returned by a reflected method back to its flags.
    /// </summary>
    public static int Unbox(object purpose)
        => Convert.ToInt32(purpose);

    /// <summary>
    /// Declares the given model capabilities on a fixture deployment.
    /// </summary>
    /// <remarks>
    /// Fixtures used to set the deployment's purpose flags directly. Capabilities replaced them, and the
    /// framework projects a stored purpose onto these same features when it reads a legacy record, so
    /// declaring them here is the shape the migrations now see.
    /// </remarks>
    public static AIDeployment Declaring(this AIDeployment deployment, params string[] features)
    {
        deployment.Put(new AIDeploymentMetadata
        {
            Features = features,
        });

        return deployment;
    }
}
