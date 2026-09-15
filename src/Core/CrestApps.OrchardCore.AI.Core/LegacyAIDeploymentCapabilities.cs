using CrestApps.Core;
using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Projects a deployment's legacy purpose onto the model capabilities that replaced it.
/// </summary>
/// <remarks>
/// <para>
/// The framework's own <see cref="AIDeploymentPurposeCompatibility"/> does most of this, mapping each
/// purpose onto the one capability that named the same thing. It stops short for the chat and utility
/// purposes: it grants text generation and nothing else, because those are the only two purposes that never
/// named a single capability. A deployment on the legacy purpose was driven through a chat client that
/// called tools and streamed its response, with no switch to turn either off, so tool calling and streaming
/// are part of what the purpose meant even though it never spelled them out.
/// </para>
/// <para>
/// Declaring only text generation would therefore migrate a working site into one where tools and streamed
/// responses are silently off, which reads as the upgrade having broken them. This type adds the two the
/// framework cannot infer, so every path that reads a legacy purpose -- the store migrations, recipe
/// imports, and the configuration and API surfaces -- ends up declaring the same set. Whatever a particular
/// model cannot really do stays one checkbox away in the deployment editor.
/// </para>
/// </remarks>
public static class LegacyAIDeploymentCapabilities
{
    /// <summary>
    /// The capabilities a chat or utility deployment is credited with.
    /// </summary>
    /// <remarks>
    /// Text generation is what the framework already infers; it is named here so the set reads as a whole
    /// and so the projection still covers it if the framework ever stops.
    /// </remarks>
    public static readonly string[] InteractiveFeatures =
    [
        AIDeploymentFeatureNames.TextGeneration,
        AIDeploymentFeatureNames.ToolCalling,
        AIDeploymentFeatureNames.Streaming,
    ];

    private static readonly string[] _interactivePurposeNames = ["Chat", "Utility"];

    /// <summary>
    /// The historical bit positions of the chat and utility purposes, for records that stored the flags
    /// numerically rather than by name.
    /// </summary>
    private const int _interactivePurposeFlags = (1 << 0) | (1 << 1);

    /// <summary>
    /// Gets the capabilities the given legacy purposes imply that the deployment does not already declare.
    /// </summary>
    /// <param name="legacyPurposes">
    /// The purpose names the record carried, in any of the forms the field has taken: one name, a
    /// comma-separated set, the numeric flags, or several entries. Names that are not recognized are
    /// ignored.
    /// </param>
    /// <param name="declaredFeatures">The capabilities the deployment already declares, if any.</param>
    public static IReadOnlyList<string> GetImpliedFeatures(
        IEnumerable<string> legacyPurposes,
        IEnumerable<string> declaredFeatures = null)
    {
        var purposes = legacyPurposes as IReadOnlyCollection<string> ?? legacyPurposes?.ToArray() ?? [];

        if (purposes.Count == 0)
        {
            return [];
        }

        var declared = declaredFeatures as IReadOnlyCollection<string> ?? declaredFeatures?.ToArray() ?? [];
        var features = AIDeploymentPurposeCompatibility.GetImpliedFeatures(purposes, declared).ToList();

        if (!NamesInteractivePurpose(purposes))
        {
            return features;
        }

        // A deployment that declares realtime is a speech-to-speech model stored under the chat purpose,
        // which is why the framework withholds text generation from it -- such a model answers a text
        // completion with an HTTP 400. The rest of the set is withheld on the same grounds: for that model
        // the purpose says only that it is conversational, and the migration should not guess past it.
        if (Declares(declared, AIDeploymentFeatureNames.Realtime))
        {
            return features;
        }

        foreach (var feature in InteractiveFeatures)
        {
            if (!Declares(declared, feature) && !Declares(features, feature))
            {
                features.Add(feature);
            }
        }

        return features;
    }

    /// <summary>
    /// Declares on the deployment the capabilities its legacy purposes imply, merging into whatever it
    /// already declares.
    /// </summary>
    /// <returns><see langword="true"/> when the deployment's metadata was changed.</returns>
    public static bool Normalize(AIDeployment deployment, IEnumerable<string> legacyPurposes)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        var hasMetadata = deployment.TryGet<AIDeploymentMetadata>(out var metadata);
        var implied = GetImpliedFeatures(legacyPurposes, hasMetadata ? metadata.Features : null);

        if (implied.Count == 0)
        {
            // Still hand the record to the framework: it clears the legacy purpose field and applies any
            // read-time rule this type does not reimplement.
            return AIDeploymentPurposeCompatibility.Normalize(deployment, legacyPurposes);
        }

        var changed = AIDeploymentPurposeCompatibility.Normalize(deployment, legacyPurposes);

        deployment.Alter<AIDeploymentMetadata>(current =>
        {
            // Re-checked against the metadata as the framework left it, not as it was read above: the
            // framework has just added the capability it maps the purpose onto, so filtering here is what
            // keeps a second pass over the same record from declaring anything twice.
            var missing = implied.Where(feature => !Declares(current.Features, feature)).ToArray();

            if (missing.Length == 0)
            {
                return;
            }

            current.Features = current.Features is { Length: > 0 }
                ? [.. current.Features, .. missing]
                : missing;

            changed = true;
        });

        return changed;
    }

    /// <summary>
    /// Determines whether any of the given legacy purpose names is the chat or utility purpose.
    /// </summary>
    /// <remarks>
    /// The names are matched as substrings so a combined value such as <c>"Chat, Utility"</c> is recognized
    /// the same as the single names, which is the shape a flags enum serializes to.
    /// </remarks>
    private static bool NamesInteractivePurpose(IEnumerable<string> legacyPurposes)
    {
        foreach (var purpose in legacyPurposes)
        {
            if (string.IsNullOrWhiteSpace(purpose))
            {
                continue;
            }

            if (int.TryParse(purpose, out var flags))
            {
                if ((flags & _interactivePurposeFlags) != 0)
                {
                    return true;
                }

                continue;
            }

            foreach (var name in _interactivePurposeNames)
            {
                if (purpose.Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool Declares(IEnumerable<string> features, string feature)
        => features is not null && features.Contains(feature, StringComparer.OrdinalIgnoreCase);
}
