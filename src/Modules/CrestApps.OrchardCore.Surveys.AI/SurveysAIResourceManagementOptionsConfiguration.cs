using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Surveys.AI;

/// <summary>
/// Declares the scripts of Surveys AI.
/// </summary>
internal sealed class SurveysAIResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    /// <summary>
    /// The name of the scoring help script.
    /// </summary>
    public const string ScoringHelpScript = "surveys-ai-scoring-help";

    private static readonly ResourceManifest _manifest;

    static SurveysAIResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript(ScoringHelpScript)
            .SetUrl("~/CrestApps.OrchardCore.Surveys.AI/scripts/surveys-ai-scoring-help.min.js", "~/CrestApps.OrchardCore.Surveys.AI/scripts/surveys-ai-scoring-help.js")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc />
    public void Configure(ResourceManagementOptions options)
        => options.ResourceManifests.Add(_manifest);
}
