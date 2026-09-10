using CrestApps.Core;
using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// The deployment purpose flags as they were written into stored documents before the framework replaced
/// the purpose with model capabilities.
/// </summary>
/// <remarks>
/// <para>
/// The framework enum is gone, but the documents these migrations upgrade were written while it existed, so
/// the migrations still need to name the values they are reading. Keeping a private copy here is what lets
/// them keep translating those old shapes without reviving the concept anywhere a caller could reach it.
/// </para>
/// <para>
/// The values are the historical bit positions, shared by both legacy enums that ever occupied the field.
/// They must not be renumbered: a stored document can carry the numeric form.
/// </para>
/// </remarks>
[Flags]
internal enum LegacyAIDeploymentPurpose
{
    None = 0,
    Chat = 1 << 0,
    Utility = 1 << 1,
    Embedding = 1 << 2,
    Image = 1 << 3,
    SpeechToText = 1 << 4,
    TextToSpeech = 1 << 5,
    Vision = 1 << 6,
}

/// <summary>
/// Translates <see cref="LegacyAIDeploymentPurpose"/> into the model capabilities that replaced it.
/// </summary>
internal static class LegacyAIDeploymentPurposeExtensions
{
    private static readonly LegacyAIDeploymentPurpose _allSupportedPurposes = Enum.GetValues<LegacyAIDeploymentPurpose>()
        .Where(static purpose => purpose != LegacyAIDeploymentPurpose.None)
        .Aggregate(LegacyAIDeploymentPurpose.None, static (current, purpose) => current | purpose);

    public static bool Supports(this LegacyAIDeploymentPurpose value, LegacyAIDeploymentPurpose purpose)
        => purpose != LegacyAIDeploymentPurpose.None && (value & purpose) == purpose;

    public static bool IsValidSelection(this LegacyAIDeploymentPurpose value)
        => value != LegacyAIDeploymentPurpose.None && (value & ~_allSupportedPurposes) == 0;

    public static IEnumerable<LegacyAIDeploymentPurpose> GetSupportedPurposes(this LegacyAIDeploymentPurpose value)
        => Enum.GetValues<LegacyAIDeploymentPurpose>().Where(purpose => value.Supports(purpose));

    /// <summary>
    /// Gets the capability features a legacy purpose implies.
    /// </summary>
    /// <remarks>
    /// This mirrors the framework's own read-time projection: the opt-in features are added whenever the
    /// corresponding purpose is present, while text generation — which is opt-out — is added for the chat
    /// and utility purposes only when the deployment does not also declare realtime, because a
    /// speech-to-speech deployment answers a text completion with an HTTP 400.
    /// </remarks>
    public static IReadOnlyList<string> ToFeatureNames(this LegacyAIDeploymentPurpose purpose, IEnumerable<string> declaredFeatures = null)
        => AIDeploymentPurposeCompatibility.GetImpliedFeatures([purpose.ToString()], declaredFeatures);

    /// <summary>
    /// Declares on the deployment the capabilities implied by a legacy purpose, merging into whatever it
    /// already declares.
    /// </summary>
    /// <returns><see langword="true"/> when the deployment's metadata was changed.</returns>
    public static bool ApplyTo(this LegacyAIDeploymentPurpose purpose, AIDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        deployment.Properties ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        var hasMetadata = deployment.TryGet<AIDeploymentMetadata>(out var metadata);
        var implied = purpose.ToFeatureNames(hasMetadata ? metadata.Features : null);

        if (implied.Count == 0)
        {
            return false;
        }

        metadata ??= new AIDeploymentMetadata();

        metadata.Features = metadata.Features is { Length: > 0 }
            ? [.. metadata.Features, .. implied]
            : [.. implied];

        deployment.Put(metadata);

        return true;
    }

    /// <summary>
    /// Determines whether a deployment declares the capabilities a legacy purpose stood for.
    /// </summary>
    /// <remarks>
    /// The chat and utility purposes both map onto text generation, which is opt-out: a deployment that
    /// declares no capability metadata at all is text capable. Every other purpose maps onto an opt-in
    /// feature the deployment has to declare.
    /// </remarks>
    public static bool IsSupportedBy(this LegacyAIDeploymentPurpose purpose, AIDeployment deployment)
    {
        if (deployment is null || purpose == LegacyAIDeploymentPurpose.None)
        {
            return false;
        }

        var hasMetadata = deployment.TryGet<AIDeploymentMetadata>(out var metadata) && metadata.Features is { Length: > 0 };

        foreach (var single in purpose.GetSupportedPurposes())
        {
            var features = single.ToFeatureNames();

            if (features.Count == 0)
            {
                continue;
            }

            foreach (var feature in features)
            {
                var isTextGeneration = string.Equals(feature, AIDeploymentFeatureNames.TextGeneration, StringComparison.OrdinalIgnoreCase);

                if (!hasMetadata)
                {
                    // An unconstrained deployment is assumed text capable and nothing else.
                    if (!isTextGeneration)
                    {
                        return false;
                    }

                    continue;
                }

                if (!metadata.SupportsFeature(feature))
                {
                    // Realtime deployments are stored as the chat purpose with no text generation. They are
                    // conversational, but they cannot serve a text completion, which is what the chat and
                    // utility purposes meant here.
                    return false;
                }
            }
        }

        return true;
    }
}
