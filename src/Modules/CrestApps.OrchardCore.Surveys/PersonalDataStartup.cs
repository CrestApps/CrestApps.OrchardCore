using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Surveys.PersonalData;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Adds survey responses and invitations to personal data exports and erasure.
/// </summary>
public sealed class PersonalDataStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddPersonalDataContributor<SurveysPersonalDataContributor>();
}
