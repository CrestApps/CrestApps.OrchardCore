using CrestApps.OrchardCore.Core.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Lets the dialer profile editors' number inputs bind and render the same in every culture, such as a decimal-comma one.
/// </summary>
public sealed class InvariantNumberInputsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddInvariantNumberInputs();
}
