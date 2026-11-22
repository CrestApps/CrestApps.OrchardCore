using CrestApps.OrchardCore.Surveys.Core.Controls;
using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Defines the script resources of the built-in question types, which the runner registers for the types that declare
/// <c>ClientScript</c>.
/// </summary>
public sealed class SurveysResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static SurveysResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript(SurveyControlScripts.Controls)
            .SetUrl("~/CrestApps.OrchardCore.Surveys/scripts/survey-controls.min.js", "~/CrestApps.OrchardCore.Surveys/scripts/survey-controls.js")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc />
    public void Configure(ResourceManagementOptions options)
        => options.ResourceManifests.Add(_manifest);
}
