using CrestApps.OrchardCore.ContentTransfer.OpenXml.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContentTransfer.OpenXml;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IContentTransferFileFormatProvider, ExcelContentTransferFileFormatProvider>();
    }
}
