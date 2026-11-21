using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Asks respondents who are not signed in to pass a CAPTCHA when a survey requires it and Orchard Core ReCaptcha is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.ReCaptcha)]
public sealed class CaptchaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddSurveyAccessRule<CaptchaRule>();
}
