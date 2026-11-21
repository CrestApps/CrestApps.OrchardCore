using CrestApps.OrchardCore.Surveys.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Counts survey responses for OpenTelemetry.
/// </summary>
public sealed class TelemetryStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddSurveyResponseObserver<SurveysTelemetryObserver>();
}
