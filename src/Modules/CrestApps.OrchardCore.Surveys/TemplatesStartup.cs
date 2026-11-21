using CrestApps.OrchardCore.Surveys.Drivers;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Registers the survey template pages.
/// </summary>
public sealed class TemplatesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddDisplayDriver<SurveyTemplate, SurveyTemplateDisplayDriver>();
}
