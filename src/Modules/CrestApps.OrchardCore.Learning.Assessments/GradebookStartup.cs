using CrestApps.OrchardCore.Learning.Assessments.Drivers;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Navigation;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Assessments;

/// <summary>
/// Registers grade scales and the course gradebook.
/// </summary>
[Feature(AssessmentsConstants.Features.Gradebook)]
public sealed class GradebookStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<GradeScale, GradeScaleRowDisplayDriver>();
        services.AddNavigationProvider<GradebookAdminMenu>();
    }
}
