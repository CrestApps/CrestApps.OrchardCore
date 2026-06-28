using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.PhoneNumbers;

/// <summary>
/// Registers phone number services for the Phone Numbers feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IPhoneNumberService, DefaultPhoneNumberService>();
    }
}
